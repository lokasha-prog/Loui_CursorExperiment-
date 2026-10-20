using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Security;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.Settings;

public sealed record RoleDto(Guid Id, string Name, Dictionary<string, string> DisplayName, string? Description, bool IsSystemRole, IReadOnlyList<string> Permissions, int UserCount);

public sealed record ListRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;

public sealed record SaveRoleCommand(Guid? Id, Dictionary<string, string> DisplayName, string? Description, IReadOnlyList<string> Permissions) : IRequest<Result<RoleDto>>;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Result>;

public static class CustomRoles
{
    /// <summary>Only a system administrator may manage accounts and roles, so no custom role can hand that power out.</summary>
    public static readonly string[] Forbidden = [Permissions.UserManagementManage];

    public static IReadOnlyList<string> Grantable() => RolePermissions.AllPermissionNames().Where(p => !Forbidden.Contains(p)).OrderBy(p => p).ToList();

    public static string Slug(string english)
    {
        var slug = Regex.Replace(english.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return $"custom_{(slug.Length > 0 ? slug : "role")}"[..Math.Min(40, $"custom_{(slug.Length > 0 ? slug : "role")}".Length)];
    }
}

public sealed class SaveRoleValidator : AbstractValidator<SaveRoleCommand>
{
    public SaveRoleValidator()
    {
        RuleFor(x => x.DisplayName).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 80).WithMessage("Give the role an English name (up to 80 characters).");
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Permissions).Must(p => p is not null && p.Count > 0).WithMessage("Choose at least one permission.");
        RuleFor(x => x.Permissions).Must(p => p.All(CustomRoles.Grantable().Contains)).WithMessage("One of the chosen permissions cannot be given to a custom role.");
    }
}

public sealed class ListRolesHandler(IAppDbContext db) : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(ListRolesQuery request, CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var counts = await db.AppUserRoles.AsNoTracking().GroupBy(r => r.RoleId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        return Result<IReadOnlyList<RoleDto>>.Success(roles.Select(r => new RoleDto(
            r.Id, r.Name, r.DisplayName, r.Description, r.IsSystemRole,
            r.IsSystemRole ? RolePermissions.ForRoles([r.Name]).OrderBy(p => p).ToList() : r.Permissions.OrderBy(p => p).ToList(),
            counts.GetValueOrDefault(r.Id))).ToList());
    }
}

public sealed class SaveRoleHandler(IAppDbContext db) : IRequestHandler<SaveRoleCommand, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(SaveRoleCommand request, CancellationToken ct)
    {
        var names = request.DisplayName.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary(n => n.Key, n => n.Value.Trim());
        Role role;
        if (request.Id is { } id)
        {
            var found = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Role not found.");
            }

            if (found.IsSystemRole)
            {
                return Error.InvalidState("Built-in roles cannot be changed. Make a custom role instead.");
            }

            role = found;
        }
        else
        {
            var name = CustomRoles.Slug(names["en"]);
            if (await db.Roles.AnyAsync(r => r.Name == name, ct))
            {
                return Error.Conflict("A role with this name already exists.", "display_name");
            }

            role = new Role { Name = name, IsSystemRole = false };
            db.Roles.Add(role);
        }

        role.DisplayName = names;
        role.Description = request.Description?.Trim();
        role.Permissions = request.Permissions.Distinct().OrderBy(p => p).ToList();
        await db.SaveChangesAsync(ct);
        var users = await db.AppUserRoles.CountAsync(r => r.RoleId == role.Id, ct);
        return Result<RoleDto>.Success(new RoleDto(role.Id, role.Name, role.DisplayName, role.Description, false, role.Permissions, users));
    }
}

public sealed class DeleteRoleHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (role is null)
        {
            return Result.Failure(Error.NotFound("Role not found."));
        }

        if (role.IsSystemRole)
        {
            return Result.Failure(Error.InvalidState("Built-in roles cannot be removed."));
        }

        if (await db.AppUserRoles.AnyAsync(r => r.RoleId == role.Id, ct))
        {
            return Result.Failure(Error.Conflict("People still have this role. Change their role first."));
        }

        role.IsDeleted = true;
        role.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
