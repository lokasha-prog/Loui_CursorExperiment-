using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Attendance;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed record RoomRatioDto(
    Guid RoomId,
    Dictionary<string, string> Name,
    int Capacity,
    int RatioStaffToStudent,
    int StudentsPresent,
    int StaffPresent,
    int StaffRequired,
    bool IsBreach);

public sealed record LiveRatioQuery : IRequest<Result<IReadOnlyList<RoomRatioDto>>>;

internal static class RatioCalculator
{
    /// <summary>Students present divided by staff present, against the room's allowed students-per-staff (spec 02 §3.3).</summary>
    public static async Task<List<RoomRatioDto>> ForRoomsAsync(
        IAppDbContext db, DateOnly today, IReadOnlyList<Guid>? onlyRooms, CancellationToken ct)
    {
        var rooms = db.Rooms.AsNoTracking().Where(r => r.IsActive);
        if (onlyRooms is not null)
        {
            rooms = rooms.Where(r => onlyRooms.Contains(r.Id));
        }

        var roomList = await rooms.OrderBy(r => r.CreatedAt).ToListAsync(ct);
        var present = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == today && a.CheckInAt != null && a.CheckOutAt == null && a.RoomId != null)
            .Select(a => new { a.PersonType, a.PersonId, RoomId = a.RoomId!.Value })
            .ToListAsync(ct);

        // Staff on an approved leave that removes them from the ratio are not counted even if they scanned in.
        var staffIds = present.Where(p => p.PersonType == PersonTypes.Staff).Select(p => p.PersonId).ToList();
        var excluded = staffIds.Count == 0
            ? []
            : await db.LeaveRequests.AsNoTracking()
                .Where(l => l.PersonType == PersonTypes.Staff && staffIds.Contains(l.PersonId)
                    && l.Status == LeaveStatuses.Approved && l.StartDate <= today && l.EndDate >= today && l.LeaveType!.AffectsRatio)
                .Select(l => l.PersonId)
                .ToListAsync(ct);

        return roomList.Select(room =>
        {
            var students = present.Count(p => p.RoomId == room.Id && p.PersonType == PersonTypes.Student);
            var staff = present.Count(p => p.RoomId == room.Id && p.PersonType == PersonTypes.Staff && !excluded.Contains(p.PersonId));
            var required = (int)Math.Ceiling(students / (double)room.RatioStaffToStudent);
            return new RoomRatioDto(room.Id, room.Name, room.Capacity, room.RatioStaffToStudent, students, staff, required, staff < required);
        }).ToList();
    }
}

internal static class RatioMonitor
{
    /// <summary>
    /// Tells the rest of the system an attendance change happened, and keeps the shortage history: a shortage opens when a room
    /// first lacks staff (one alert), is updated while it continues, and closes when the room is covered again.
    /// </summary>
    public static async Task PublishAsync(
        IAppDbContext db, IPublisher publisher, IClock clock, Guid tenantId, DateOnly today, Guid? roomId,
        ResolvedPerson person, string status, CancellationToken ct)
    {
        if (roomId is not null)
        {
            await TrackRoomAsync(db, publisher, clock, tenantId, today, roomId.Value, ct);
        }

        await publisher.Publish(new AttendanceChanged(tenantId, person.Type, person.Id, roomId, status), ct);
    }

    private static async Task TrackRoomAsync(
        IAppDbContext db, IPublisher publisher, IClock clock, Guid tenantId, DateOnly today, Guid roomId, CancellationToken ct)
    {
        var ratio = (await RatioCalculator.ForRoomsAsync(db, today, [roomId], ct)).FirstOrDefault();
        if (ratio is null)
        {
            return;
        }

        var open = await db.RatioBreaches.FirstOrDefaultAsync(b => b.RoomId == roomId && b.ResolvedAt == null, ct);
        if (ratio.IsBreach)
        {
            if (open is null)
            {
                db.RatioBreaches.Add(new RatioBreach
                {
                    RoomId = roomId, DetectedAt = clock.UtcNow, StudentsPresent = ratio.StudentsPresent,
                    StaffPresent = ratio.StaffPresent, StaffRequired = ratio.StaffRequired,
                });
                await db.SaveChangesAsync(ct);
                await publisher.Publish(new RatioBreachDetected(tenantId, roomId, ratio.StudentsPresent, ratio.StaffPresent, ratio.StaffRequired), ct);
            }
            else if (open.StudentsPresent != ratio.StudentsPresent || open.StaffPresent != ratio.StaffPresent || open.StaffRequired != ratio.StaffRequired)
            {
                open.StudentsPresent = ratio.StudentsPresent;
                open.StaffPresent = ratio.StaffPresent;
                open.StaffRequired = ratio.StaffRequired;
                await db.SaveChangesAsync(ct);
            }
        }
        else if (open is not null)
        {
            open.ResolvedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await publisher.Publish(new RatioRestored(tenantId, roomId), ct);
        }
    }
}

public sealed record RatioBreachDto(
    Guid Id,
    Guid RoomId,
    Dictionary<string, string>? RoomName,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ResolvedAt,
    int StudentsPresent,
    int StaffPresent,
    int StaffRequired,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record RatioHistoryQuery(Guid? RoomId, string? Cursor, int? Limit) : IRequest<Result<CursorPage<RatioBreachDto>>>;

public sealed class RatioHistoryHandler(IAppDbContext db) : IRequestHandler<RatioHistoryQuery, Result<CursorPage<RatioBreachDto>>>
{
    public async Task<Result<CursorPage<RatioBreachDto>>> Handle(RatioHistoryQuery request, CancellationToken ct)
    {
        var breaches = db.RatioBreaches.AsNoTracking();
        if (request.RoomId is { } roomId)
        {
            breaches = breaches.Where(b => b.RoomId == roomId);
        }

        var page = await breaches.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            b => new RatioBreachDto(
                b.Id, b.RoomId, db.Rooms.Where(r => r.Id == b.RoomId).Select(r => r.Name).FirstOrDefault(),
                b.DetectedAt, b.ResolvedAt, b.StudentsPresent, b.StaffPresent, b.StaffRequired, b.CreatedAt),
            ct);
        return Result<CursorPage<RatioBreachDto>>.Success(page);
    }
}

public sealed class LiveRatioHandler(IAppDbContext db, IAccessScope scope, CentreCalendar calendar)
    : IRequestHandler<LiveRatioQuery, Result<IReadOnlyList<RoomRatioDto>>>
{
    public async Task<Result<IReadOnlyList<RoomRatioDto>>> Handle(LiveRatioQuery request, CancellationToken ct)
    {
        var visible = await scope.GetVisibleRoomIdsAsync(ct);
        var ratios = await RatioCalculator.ForRoomsAsync(db, calendar.Today, visible, ct);
        return Result<IReadOnlyList<RoomRatioDto>>.Success(ratios);
    }
}
