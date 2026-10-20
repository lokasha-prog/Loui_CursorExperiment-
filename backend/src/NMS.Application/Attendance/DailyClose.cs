using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

/// <summary>
/// Scheduled job, run for one centre at a time: a child on an approved leave that counts as an excused absence is marked
/// absent for today without anyone having to do it. Safe to run any number of times.
/// </summary>
public sealed record RunDailyCloseCommand : IRequest<Result<int>>;

public sealed class RunDailyCloseHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<RunDailyCloseCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunDailyCloseCommand request, CancellationToken ct)
    {
        var today = calendar.Today;
        var onLeave = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.PersonType == PersonTypes.Student && l.Status == LeaveStatuses.Approved
                && l.StartDate <= today && l.EndDate >= today && l.LeaveType!.AffectsAttendance)
            .Select(l => l.PersonId).Distinct().ToListAsync(ct);
        if (onLeave.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var already = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == today && a.PersonType == PersonTypes.Student && onLeave.Contains(a.PersonId))
            .Select(a => a.PersonId).ToListAsync(ct);
        var students = await db.Students.AsNoTracking()
            .Where(s => onLeave.Contains(s.Id) && !already.Contains(s.Id) && s.Status == StudentStatuses.Active)
            .Select(s => new { s.Id, s.RoomId }).ToListAsync(ct);

        foreach (var s in students)
        {
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                PersonType = PersonTypes.Student, PersonId = s.Id, RoomId = s.RoomId, Date = today,
                Status = AttendanceStatuses.Absent, Notes = "On approved leave",
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(students.Count);
    }
}
