using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed record DashboardSummaryDto(
    DateOnly Date,
    int ActiveStudents,
    int StudentsPresent,
    int StudentsAbsent,
    int StudentsNotYetArrived,
    int StudentsOnLeave,
    int ActiveStaff,
    int StaffPresent,
    int RoomsInBreach,
    int PendingLeaveRequests);

public sealed record DashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;

public sealed class DashboardSummaryHandler(IAppDbContext db, IAccessScope scope, CentreCalendar calendar)
    : IRequestHandler<DashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    public async Task<Result<DashboardSummaryDto>> Handle(DashboardSummaryQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        var own = await scope.GetOwnStudentIdsAsync(ct);

        var students = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
        if (rooms is not null)
        {
            students = students.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
        }

        if (own is not null)
        {
            students = students.Where(s => own.Contains(s.Id));
        }

        var studentIds = students.Select(s => s.Id);
        var activeStudents = await students.CountAsync(ct);
        var records = db.AttendanceRecords.AsNoTracking().Where(a => a.Date == today && a.PersonType == PersonTypes.Student && studentIds.Contains(a.PersonId));
        var present = await records.CountAsync(a => a.CheckInAt != null, ct);
        var absent = await records.CountAsync(a => a.Status == AttendanceStatuses.Absent, ct);
        var onLeave = await db.LeaveRequests.AsNoTracking().CountAsync(
            l => l.PersonType == PersonTypes.Student && l.Status == LeaveStatuses.Approved
                && l.StartDate <= today && l.EndDate >= today && studentIds.Contains(l.PersonId), ct);

        var unrestricted = rooms is null && own is null;
        var activeStaff = unrestricted ? await db.Staff.AsNoTracking().CountAsync(s => s.Status == "active", ct) : 0;
        var staffPresent = unrestricted
            ? await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date == today && a.PersonType == PersonTypes.Staff && a.CheckInAt != null && a.CheckOutAt == null, ct)
            : 0;
        var breaches = own is null ? (await RatioCalculator.ForRoomsAsync(db, today, rooms, ct)).Count(r => r.IsBreach) : 0;
        var pending = unrestricted ? await db.LeaveRequests.AsNoTracking().CountAsync(l => l.Status == LeaveStatuses.Submitted, ct) : 0;

        return Result<DashboardSummaryDto>.Success(new DashboardSummaryDto(
            today, activeStudents, present, absent, Math.Max(0, activeStudents - present - absent - onLeave), onLeave,
            activeStaff, staffPresent, breaches, pending));
    }
}
