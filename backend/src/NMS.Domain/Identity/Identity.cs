using NMS.Domain.Common;

namespace NMS.Domain.Identity;

public static class SystemRoles
{
    public const string SuperAdmin = "super_admin";
    public const string CentreAdmin = "centre_admin";
    public const string Teacher = "teacher";
    public const string Parent = "parent";
    public const string Receptionist = "receptionist";
    public const string Accounts = "accounts";
    public const string It = "it";
    public const string Marketing = "marketing";

    public static readonly IReadOnlyList<string> All =
        [SuperAdmin, CentreAdmin, Teacher, Parent, Receptionist, Accounts, It, Marketing];
}

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Dictionary<string, string> Name { get; set; } = [];

    public string Slug { get; set; } = string.Empty;

    public string DeploymentMode { get; set; } = "saas";

    public Guid? ParentTenantId { get; set; }

    public string Status { get; set; } = "active";

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? KhdaLicenseNumber { get; set; }

    public string[] WeekendDays { get; set; } = ["fri", "sat"];

    public string Timezone { get; set; } = "Asia/Dubai";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>A person who can sign in. Credentials are owned by NMS (ADR-0005). Id is the JWT "sub" claim.</summary>
public class AppUser : TenantEntity
{
    public string Email { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public Dictionary<string, string>? DisplayName { get; set; }

    public string PreferredLanguage { get; set; } = "en";

    public string? Phone { get; set; }

    public string Status { get; set; } = "active";

    public DateTimeOffset? LastLoginAt { get; set; }

    public string? PasswordHash { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public List<AppUserRole> Roles { get; set; } = [];

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is not null && LockoutEnd > now;

    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEnd = now.Add(lockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
        LastLoginAt = now;
    }
}

public class Role : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> DisplayName { get; set; } = [];

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public List<string> Permissions { get; set; } = [];
}

public class AppUserRole : ITenantEntity
{
    public Guid AppUserId { get; set; }

    public Guid RoleId { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public Guid AssignedBy { get; set; }

    public AppUser? AppUser { get; set; }

    public Role? Role { get; set; }
}

/// <summary>
/// Rotating refresh token (spec 09 §3.4). Only a hash is stored. Presenting an already-used token
/// revokes the whole family, because that indicates theft.
/// </summary>
public class RefreshToken : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }
}

/// <summary>
/// Maps a sign-in e-mail address to the centre(s) it belongs to, so people never have to type a centre code.
/// It deliberately has no tenant column and no row-level security: it is read before the centre is known.
/// It holds only the e-mail, centre and user ids, never credentials.
/// </summary>
public class LoginDirectoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public Guid CentreId { get; set; }

    public Guid UserId { get; set; }
}

/// <summary>One row per centre holding the settings that are not on the centre record itself: branding, attendance timing and what parents may see.</summary>
public class CentreSettings : TenantEntity
{
    public string? Address { get; set; }

    public string? Website { get; set; }

    /// <summary>Hex colour such as #0E7C86 used for the centre's branding.</summary>
    public string? BrandColor { get; set; }

    public Guid? LogoMediaId { get; set; }

    /// <summary>Minutes after the programme starts before a check-in counts as late. Empty means the system default.</summary>
    public int? LateGraceMinutes { get; set; }

    public int? EarlyLeaveGraceMinutes { get; set; }

    public bool ParentsSeeInvoices { get; set; } = true;

    /// <summary>Secret in the calendar subscription link families add to their phone. Changing it switches the old link off.</summary>
    public string? CalendarFeedToken { get; set; }

    /// <summary>Critical alerts (ratio shortage, unexpected absence) are also sent by text message when this is on. It costs money per message, so it is off until the centre chooses.</summary>
    public bool SmsEnabled { get; set; }

    public int SmsMonthlyQuota { get; set; } = 200;
}
