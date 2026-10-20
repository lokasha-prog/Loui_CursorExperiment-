using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }

    public bool IsInitialized => TenantId != Guid.Empty;
}

/// <summary>
/// Applies the visibility rules from spec 09 §2.4. Teachers see only the rooms they are assigned to;
/// parents see only their own children. Everyone else is unrestricted (null).
/// </summary>
public sealed class AccessScope(IAppDbContext db, ICurrentUser user, IClock clock) : IAccessScope
{
    private static readonly string[] UnrestrictedRoles =
        [SystemRoles.SuperAdmin, SystemRoles.CentreAdmin, SystemRoles.It, SystemRoles.Receptionist];

    private bool _roomsLoaded;
    private IReadOnlyList<Guid>? _rooms;
    private bool _studentsLoaded;
    private IReadOnlyList<Guid>? _students;

    public async Task<IReadOnlyList<Guid>?> GetVisibleRoomIdsAsync(CancellationToken cancellationToken)
    {
        if (_roomsLoaded)
        {
            return _rooms;
        }

        if (user.IsInRole(SystemRoles.Teacher) && !user.Roles.Any(r => UnrestrictedRoles.Contains(r)))
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
            var userId = user.UserId;
            _rooms = await db.StaffRoomAssignments.AsNoTracking()
                .Where(a => db.Staff.Any(s => s.Id == a.StaffId && s.AppUserId == userId)
                    && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
                .Select(a => a.RoomId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        _roomsLoaded = true;
        return _rooms;
    }

    public async Task<IReadOnlyList<Guid>?> GetOwnStudentIdsAsync(CancellationToken cancellationToken)
    {
        if (_studentsLoaded)
        {
            return _students;
        }

        var isParentOnly = user.IsInRole(SystemRoles.Parent)
            && !user.Roles.Any(r => UnrestrictedRoles.Contains(r) || r == SystemRoles.Teacher);
        if (isParentOnly)
        {
            var userId = user.UserId;
            _students = await db.StudentGuardians.AsNoTracking()
                .Where(l => l.Guardian!.AppUserId == userId)
                .Select(l => l.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        _studentsLoaded = true;
        return _students;
    }
}
