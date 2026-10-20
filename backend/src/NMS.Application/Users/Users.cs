using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Auth;
using NMS.Application.Common;
using NMS.Domain.Identity;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string PreferredLanguage,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListUsersQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<UserDto>>>;

public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    IReadOnlyList<string> Roles,
    string? PreferredLanguage,
    Guid? StaffId,
    Guid? GuardianId) : IRequest<Result<UserDto>>;

public sealed record SetUserStatusCommand(Guid Id, string Status) : IRequest<Result<UserDto>>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator(IOptions<AuthOptions> options)
    {
        var minLength = options.Value.MinPasswordLength;
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Password)
            .MinimumLength(minLength).WithMessage($"The password must be at least {minLength} characters.")
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit)).WithMessage("The password must contain letters and numbers.");
        RuleFor(c => c.Roles).NotEmpty().WithMessage("Choose at least one role.");
        RuleFor(c => c.PreferredLanguage!).Must(l => l is "en" or "ar").When(c => c.PreferredLanguage is not null)
            .WithMessage("Language must be en or ar.");
    }
}

public sealed class SetUserStatusValidator : AbstractValidator<SetUserStatusCommand>
{
    public SetUserStatusValidator() =>
        RuleFor(c => c.Status).Must(s => s is "active" or "suspended").WithMessage("Status must be active or suspended.");
}

public sealed class ListUsersHandler(IAppDbContext db) : IRequestHandler<ListUsersQuery, Result<CursorPage<UserDto>>>
{
    public async Task<Result<CursorPage<UserDto>>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var page = await db.AppUsers.AsNoTracking().ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            u => new UserDto(
                u.Id, u.Email, u.FirstName, u.LastName, u.PreferredLanguage, u.Status,
                u.Roles.Select(r => r.Role!.Name).ToList(), u.LastLoginAt, u.CreatedAt),
            ct);
        return Result<CursorPage<UserDto>>.Success(page);
    }
}

public sealed class CreateUserHandler(IAppDbContext db, ITenantContext tenant, IPasswordService passwords, IClock clock, ICurrentUser current)
    : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.AppUsers.AnyAsync(u => u.Email == email, ct))
        {
            return Error.Conflict("A user with this email already exists.", "email");
        }

        var roleNames = request.Roles.Distinct().ToList();
        var roles = await db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != roleNames.Count)
        {
            return Error.Validation("One or more roles do not exist.", "roles");
        }

        // Only a super administrator may hand out the super administrator role.
        if (roleNames.Contains(SystemRoles.SuperAdmin) && !current.IsInRole(SystemRoles.SuperAdmin))
        {
            return Error.Forbidden("Only a super administrator can assign this role.");
        }

        var user = new AppUser
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = request.PreferredLanguage ?? "en",
            PasswordHash = passwords.Hash(request.Password),
        };
        foreach (var role in roles)
        {
            user.Roles.Add(new AppUserRole { Role = role, AssignedAt = clock.UtcNow, AssignedBy = current.UserId });
        }

        db.AppUsers.Add(user);
        db.LoginDirectory.Add(new LoginDirectoryEntry { Email = email, CentreId = tenant.TenantId, UserId = user.Id });

        if (request.StaffId is { } staffId)
        {
            var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == staffId, ct);
            if (staff is null)
            {
                return Error.Validation("The selected staff member does not exist.", "staff_id");
            }

            staff.AppUserId = user.Id;
        }

        if (request.GuardianId is { } guardianId)
        {
            var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Id == guardianId, ct);
            if (guardian is null)
            {
                return Error.Validation("The selected guardian does not exist.", "guardian_id");
            }

            guardian.AppUserId = user.Id;
        }

        await db.SaveChangesAsync(ct);
        return Result<UserDto>.Success(ToDto(user, roleNames));
    }

    internal static UserDto ToDto(AppUser user, IReadOnlyList<string> roles) => new(
        user.Id, user.Email, user.FirstName, user.LastName, user.PreferredLanguage, user.Status, roles,
        user.LastLoginAt, user.CreatedAt);
}

public sealed class SetUserStatusHandler(IAppDbContext db, IClock clock) : IRequestHandler<SetUserStatusCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(SetUserStatusCommand request, CancellationToken ct)
    {
        var user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct);
        if (user is null)
        {
            return Error.NotFound("User not found.");
        }

        user.Status = request.Status;
        if (request.Status == "suspended")
        {
            // Suspending ends every active session immediately (spec 09 §8.3).
            var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
            tokens.ForEach(t => t.RevokedAt = clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return Result<UserDto>.Success(CreateUserHandler.ToDto(user, user.Roles.Select(r => r.Role!.Name).ToList()));
    }
}
