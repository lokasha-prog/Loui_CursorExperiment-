using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.Auth;

/// <summary>
/// Development-only helper (ADR-0006 option A). The API maps it only in the Development environment, only when
/// DevTools:Enabled is set in local secrets, and only for requests from the same computer. Nothing here is a stored or
/// shown password: each test user gets a random password nobody sees, and signing in as them skips the password.
/// </summary>
public sealed record DevTestUserDto(string Role, string Email, bool Exists);

public sealed record ListDevTestUsersQuery : IRequest<Result<IReadOnlyList<DevTestUserDto>>>;

public sealed record DevQuickLoginCommand(string Role, string? IpAddress) : IRequest<Result<AuthSession>>;

internal static class DevTestUsers
{
    /// <summary>The super administrator is the owner's real account and is never offered here.</summary>
    public static readonly IReadOnlyList<string> Roles = SystemRoles.All.Where(r => r != SystemRoles.SuperAdmin).ToList();

    public static string EmailFor(string role) => $"test.{role.Replace('_', '.')}@dev.local";

    public static async Task<Tenant?> FirstCentreAsync(IAppDbContext db, CancellationToken ct) =>
        await db.Tenants.AsNoTracking().Where(t => !t.IsDeleted && t.Status == "active").OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(ct);
}

public sealed class ListDevTestUsersHandler(IAppDbContext db, ITenantContext tenant)
    : IRequestHandler<ListDevTestUsersQuery, Result<IReadOnlyList<DevTestUserDto>>>
{
    public async Task<Result<IReadOnlyList<DevTestUserDto>>> Handle(ListDevTestUsersQuery request, CancellationToken ct)
    {
        var centre = await DevTestUsers.FirstCentreAsync(db, ct);
        if (centre is null)
        {
            return Result<IReadOnlyList<DevTestUserDto>>.Success([]);
        }

        tenant.TenantId = centre.Id;
        var existing = await db.AppUsers.AsNoTracking().Select(u => u.Email).ToListAsync(ct);
        return Result<IReadOnlyList<DevTestUserDto>>.Success(
            DevTestUsers.Roles.Select(r => new DevTestUserDto(r, DevTestUsers.EmailFor(r), existing.Contains(DevTestUsers.EmailFor(r)))).ToList());
    }
}

public sealed class DevQuickLoginHandler(
    IAppDbContext db,
    ITenantContext tenant,
    IPasswordService passwords,
    ITokenService tokens,
    IClock clock,
    IOptions<AuthOptions> options) : IRequestHandler<DevQuickLoginCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(DevQuickLoginCommand request, CancellationToken ct)
    {
        if (!DevTestUsers.Roles.Contains(request.Role))
        {
            return Error.Validation("Unknown role.", "role");
        }

        var centre = await DevTestUsers.FirstCentreAsync(db, ct);
        if (centre is null)
        {
            return Error.NotFound("Set up the centre first.");
        }

        tenant.TenantId = centre.Id;
        var email = DevTestUsers.EmailFor(request.Role);
        var user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.Role, ct);
            if (role is null)
            {
                return Error.NotFound("The role does not exist in this centre.");
            }

            // A random password that is never shown or kept: this account can only be entered through the helper.
            user = new AppUser
            {
                Email = email,
                FirstName = "Test",
                LastName = char.ToUpperInvariant(request.Role[0]) + request.Role[1..].Replace('_', ' '),
                PasswordHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(33)) + "a1"),
            };
            user.Roles.Add(new AppUserRole { Role = role, AssignedAt = clock.UtcNow });
            db.AppUsers.Add(user);
            db.LoginDirectory.Add(new LoginDirectoryEntry { Email = email, CentreId = centre.Id, UserId = user.Id });
            await db.SaveChangesAsync(ct);
            user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role).FirstAsync(u => u.Id == user.Id, ct);
        }

        if (user.Status != "active")
        {
            return Error.Forbidden("This test account is suspended.");
        }

        user.RegisterSuccessfulLogin(clock.UtcNow);
        var session = await new SessionFactory(db, tokens, clock, options).IssueAsync(user, Guid.NewGuid(), request.IpAddress, ct);
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }
}
