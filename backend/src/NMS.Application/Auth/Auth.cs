using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Security;
using NMS.Domain.Auditing;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.Auth;

/// <summary>All values come from configuration (section "Auth"); nothing is hard-coded in code.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int MaxFailedAttempts { get; set; }

    public int LockoutMinutes { get; set; }

    public int AccessTokenMinutes { get; set; }

    public int RefreshTokenDays { get; set; }

    /// <summary>
    /// A refresh token used again within this many seconds is treated as a harmless duplicate (two tabs, a quick reload)
    /// rather than theft. After the window, reuse revokes the whole session family.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; set; }

    public int MinPasswordLength { get; set; }

    /// <summary>Optional. Used when a sign-in request names no centre and several centres exist.</summary>
    public string? DefaultTenantSlug { get; set; }

    /// <summary>Secret that authorises the one-time creation of the first administrator.</summary>
    public string? BootstrapToken { get; set; }
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string hash, string password);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> roles);
}

public sealed record TenantSummaryDto(Guid Id, string Slug, Dictionary<string, string> Name);

public sealed record MeDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string PreferredLanguage,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    TenantSummaryDto Tenant);

public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    MeDto Me);

public sealed record LoginCommand(string? Tenant, string Email, string Password, string? IpAddress)
    : IRequest<Result<AuthSession>>;

public sealed record RefreshSessionCommand(string RefreshToken, string? IpAddress) : IRequest<Result<AuthSession>>;

public sealed record LogoutCommand(string? RefreshToken) : IRequest<Result>;

public sealed record GetMeQuery : IRequest<Result<MeDto>>;

public sealed record BootstrapCommand(
    string BootstrapToken,
    string TenantSlug,
    Dictionary<string, string> TenantName,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string AdminPassword) : IRequest<Result<MeDto>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(254);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class BootstrapValidator : AbstractValidator<BootstrapCommand>
{
    public BootstrapValidator(IOptions<AuthOptions> options)
    {
        var minLength = options.Value.MinPasswordLength;
        RuleFor(c => c.BootstrapToken).NotEmpty();
        RuleFor(c => c.TenantSlug).NotEmpty().Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Use lowercase letters, numbers and hyphens only.");
        RuleFor(c => c.TenantName).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
            .WithMessage("An English centre name is required.");
        RuleFor(c => c.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(c => c.AdminFirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.AdminLastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.AdminPassword)
            .MinimumLength(minLength).WithMessage($"The password must be at least {minLength} characters.")
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit)).WithMessage("The password must contain letters and numbers.");
    }
}

internal static class RefreshTokens
{
    /// <summary>Token format "{tenantId:N}.{secret}" so the tenant is known before any row-level-secured query.</summary>
    public static string Generate(Guid tenantId) =>
        $"{tenantId:N}.{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=')}";

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    public static bool TryGetTenant(string? raw, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var separator = raw?.IndexOf('.') ?? -1;
        return separator == 32 && Guid.TryParseExact(raw![..32], "N", out tenantId);
    }
}

internal sealed class SessionFactory(
    IAppDbContext db,
    ITokenService tokens,
    IClock clock,
    IOptions<AuthOptions> options)
{
    public async Task<MeDto> BuildMeAsync(AppUser user, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == user.TenantId, ct);
        var roles = user.Roles.Select(r => r.Role!.Name).OrderBy(n => n).ToList();
        var permissions = RolePermissions.ForRoles(roles).OrderBy(p => p).ToList();
        return new MeDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PreferredLanguage, roles, permissions,
            new TenantSummaryDto(tenant.Id, tenant.Slug, tenant.Name));
    }

    public async Task<AuthSession> IssueAsync(AppUser user, Guid familyId, string? ip, CancellationToken ct)
    {
        var roles = user.Roles.Select(r => r.Role!.Name).ToList();
        var access = tokens.CreateAccessToken(user, roles);
        var raw = RefreshTokens.Generate(user.TenantId);
        var now = clock.UtcNow;
        var expires = now.AddDays(options.Value.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = RefreshTokens.Hash(raw),
            CreatedAt = now,
            ExpiresAt = expires,
            CreatedByIp = ip,
        });

        return new AuthSession(access.Value, access.ExpiresAt, raw, expires, await BuildMeAsync(user, ct));
    }
}

internal static class AuditEntries
{
    public static AuditLog For(
        ITenantContext tenant, ICurrentUser user, IClock clock, string action, Guid? userId, string? email, string? ip) => new()
    {
        TenantId = tenant.TenantId,
        UserId = userId,
        UserEmail = email,
        EntityType = "app_user",
        EntityId = userId ?? Guid.Empty,
        Action = action,
        IpAddress = ip ?? user.IpAddress,
        UserAgent = user.UserAgent,
        RequestId = user.RequestId,
        OccurredAt = clock.UtcNow,
    };
}

public sealed class LoginHandler(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IPasswordService passwords,
    ITokenService tokens,
    IClock clock,
    IOptions<AuthOptions> options) : IRequestHandler<LoginCommand, Result<AuthSession>>
{
    private static readonly Error InvalidCredentials = Error.Unauthorized("Invalid email or password.");

    public async Task<Result<AuthSession>> Handle(LoginCommand request, CancellationToken ct)
    {
        var tenantRow = await ResolveTenantAsync(request.Tenant, ct);
        if (tenantRow is null)
        {
            return InvalidCredentials;
        }

        tenant.TenantId = tenantRow.Id;
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
        var now = clock.UtcNow;

        if (user is null || user.PasswordHash is null)
        {
            db.AuditLogs.Add(AuditEntries.For(tenant, currentUser, clock, AuditActions.Login, null, email, request.IpAddress));
            await db.SaveChangesAsync(ct);
            return InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return Error.Unauthorized("This account is temporarily locked. Try again later.");
        }

        if (!passwords.Verify(user.PasswordHash, request.Password))
        {
            user.RegisterFailedLogin(now, options.Value.MaxFailedAttempts, TimeSpan.FromMinutes(options.Value.LockoutMinutes));
            await db.SaveChangesAsync(ct);
            return InvalidCredentials;
        }

        if (user.Status != "active")
        {
            return Error.Forbidden("This account is not active.");
        }

        user.RegisterSuccessfulLogin(now);
        var factory = new SessionFactory(db, tokens, clock, options);
        var session = await factory.IssueAsync(user, Guid.NewGuid(), request.IpAddress, ct);
        db.AuditLogs.Add(AuditEntries.For(tenant, currentUser, clock, AuditActions.Login, user.Id, user.Email, request.IpAddress));
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }

    private async Task<Tenant?> ResolveTenantAsync(string? slug, CancellationToken ct)
    {
        slug = string.IsNullOrWhiteSpace(slug) ? options.Value.DefaultTenantSlug : slug.Trim().ToLowerInvariant();
        var tenants = db.Tenants.AsNoTracking().Where(t => !t.IsDeleted && t.Status == "active");
        if (!string.IsNullOrWhiteSpace(slug))
        {
            return await tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        }

        // Single-centre (on-premises) installs have exactly one centre, so no name is needed.
        var all = await tenants.Take(2).ToListAsync(ct);
        return all.Count == 1 ? all[0] : null;
    }
}

public sealed class RefreshSessionHandler(
    IAppDbContext db,
    ITenantContext tenant,
    ITokenService tokens,
    IClock clock,
    IOptions<AuthOptions> options) : IRequestHandler<RefreshSessionCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(RefreshSessionCommand request, CancellationToken ct)
    {
        if (!RefreshTokens.TryGetTenant(request.RefreshToken, out var tenantId))
        {
            return Error.Unauthorized("Session expired.");
        }

        tenant.TenantId = tenantId;
        var hash = RefreshTokens.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.UtcNow;
        if (stored is null)
        {
            return Error.Unauthorized("Session expired.");
        }

        var withinGrace = stored.RevokedAt is null
            && stored.UsedAt is not null
            && now - stored.UsedAt.Value <= TimeSpan.FromSeconds(options.Value.RefreshReuseGraceSeconds);

        if ((stored.UsedAt is not null || stored.RevokedAt is not null) && !withinGrace)
        {
            // A rotated token was presented again after the grace window: assume theft and end every session in the family.
            var family = await db.RefreshTokens.Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null).ToListAsync(ct);
            family.ForEach(t => t.RevokedAt = now);
            await db.SaveChangesAsync(ct);
            return Error.Unauthorized("Session expired.");
        }

        if (stored.ExpiresAt <= now)
        {
            return Error.Unauthorized("Session expired.");
        }

        var user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || user.Status != "active" || user.IsLockedOut(now))
        {
            stored.RevokedAt = now;
            await db.SaveChangesAsync(ct);
            return Error.Unauthorized("Session expired.");
        }

        stored.UsedAt ??= now;
        var session = await new SessionFactory(db, tokens, clock, options).IssueAsync(user, stored.FamilyId, request.IpAddress, ct);
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }
}

public sealed class LogoutHandler(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!RefreshTokens.TryGetTenant(request.RefreshToken, out var tenantId))
        {
            return Result.Success();
        }

        tenant.TenantId = tenantId;
        var hash = RefreshTokens.Hash(request.RefreshToken!);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var family = await db.RefreshTokens.Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null).ToListAsync(ct);
        family.ForEach(t => t.RevokedAt = now);
        db.AuditLogs.Add(AuditEntries.For(tenant, currentUser, clock, AuditActions.Logout, stored.UserId, currentUser.Email, null));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class GetMeHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser currentUser, ITokenService tokens, IClock clock, IOptions<AuthOptions> options)
    : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await db.AppUsers.AsNoTracking().Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null || tenant.TenantId != user.TenantId)
        {
            return Error.Unauthorized();
        }

        return Result<MeDto>.Success(await new SessionFactory(db, tokens, clock, options).BuildMeAsync(user, ct));
    }
}

/// <summary>
/// One-time creation of the first centre and its administrator, run by the owner (spec 08 §15.1).
/// It is not a seed: every value is supplied by the owner, and it refuses to run once a centre exists.
/// </summary>
public sealed class BootstrapHandler(
    IAppDbContext db,
    ITenantContext tenant,
    IPasswordService passwords,
    IClock clock,
    IOptions<AuthOptions> options) : IRequestHandler<BootstrapCommand, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(BootstrapCommand request, CancellationToken ct)
    {
        var configured = options.Value.BootstrapToken;
        if (string.IsNullOrWhiteSpace(configured)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(request.BootstrapToken)))
        {
            return Error.NotFound();
        }

        if (await db.Tenants.AnyAsync(ct))
        {
            return Error.Conflict("Setup has already been completed.");
        }

        var now = clock.UtcNow;
        var adminId = Guid.NewGuid();
        var newTenant = new Tenant
        {
            Name = request.TenantName,
            Slug = request.TenantSlug,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = adminId,
            UpdatedBy = adminId,
        };
        db.Tenants.Add(newTenant);
        await db.SaveChangesAsync(ct);

        tenant.TenantId = newTenant.Id;

        var roles = SystemRoleFactory.Create(adminId).ToDictionary(r => r.Name);
        db.Roles.AddRange(roles.Values);

        var admin = new AppUser
        {
            Id = adminId,
            Email = request.AdminEmail.Trim().ToLowerInvariant(),
            EmailVerified = true,
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            PasswordHash = passwords.Hash(request.AdminPassword),
            CreatedBy = adminId,
            UpdatedBy = adminId,
        };
        admin.Roles.Add(new AppUserRole { Role = roles[SystemRoles.SuperAdmin], AssignedAt = now, AssignedBy = adminId });
        db.AppUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        var me = new MeDto(
            admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.PreferredLanguage,
            [SystemRoles.SuperAdmin], RolePermissions.ForRoles([SystemRoles.SuperAdmin]).OrderBy(p => p).ToList(),
            new TenantSummaryDto(newTenant.Id, newTenant.Slug, newTenant.Name));
        return Result<MeDto>.Success(me);
    }

}

/// <summary>Builds the eight system roles with their permissions from the RBAC matrix (spec 09 §2.2).</summary>
internal static class SystemRoleFactory
{
    public static IReadOnlyList<Role> Create(Guid createdBy) =>
        SystemRoles.All.Select(name => new Role
        {
            Name = name,
            DisplayName = new Dictionary<string, string> { ["en"] = ToTitle(name) },
            IsSystemRole = true,
            Permissions = RolePermissions.ForRoles([name]).OrderBy(p => p).ToList(),
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        }).ToList();

    private static string ToTitle(string name) =>
        string.Join(' ', name.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
