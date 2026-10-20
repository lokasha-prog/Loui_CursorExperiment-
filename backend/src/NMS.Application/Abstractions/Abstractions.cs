using Microsoft.EntityFrameworkCore;
using NMS.Domain.Auditing;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;

namespace NMS.Application.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; set; }

    bool IsInitialized { get; }
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? RequestId { get; }

    bool IsInRole(string role);

    bool HasPermission(string permission);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<AppUser> AppUsers { get; }

    DbSet<Role> Roles { get; }

    DbSet<AppUserRole> AppUserRoles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<LoginDirectoryEntry> LoginDirectory { get; }

    DbSet<Student> Students { get; }

    DbSet<Guardian> Guardians { get; }

    DbSet<StudentGuardian> StudentGuardians { get; }

    DbSet<Staff> Staff { get; }

    DbSet<StaffRoomAssignment> StaffRoomAssignments { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Program> Programs { get; }

    DbSet<StudentGroup> StudentGroups { get; }

    DbSet<Label> Labels { get; }

    DbSet<StudentLabel> StudentLabels { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Restricts what the current user may see: teachers to the rooms they are assigned to,
/// parents to their own children (spec 09 §2.4). Returns null when the user is unrestricted.
/// </summary>
public interface IAccessScope
{
    Task<IReadOnlyList<Guid>?> GetVisibleRoomIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>?> GetOwnStudentIdsAsync(CancellationToken cancellationToken);
}
