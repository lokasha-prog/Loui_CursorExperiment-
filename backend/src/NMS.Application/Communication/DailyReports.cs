using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Domain.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Domain.Communication;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Communication;

public sealed class DailyReportOptions
{
    public const string Section = "DailyReports";

    /// <summary>Local time of day (HH:mm) from which each child's report for the day is written.</summary>
    public string GenerateAt { get; set; } = "15:30";
}

public sealed record DailyReportDto(
    Guid Id, Guid StudentId, string StudentName, DateOnly Date, int ActivityCount,
    DateTimeOffset? CheckInAt, DateTimeOffset? CheckOutAt, Dictionary<string, string> Summary, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListDailyReportsQuery(Guid? StudentId, string? Cursor, int? Limit) : IRequest<Result<CursorPage<DailyReportDto>>>;

/// <summary>Scheduled job, run per centre: writes each present child's report for today once the configured time has passed.</summary>
public sealed record RunDailyReportsCommand : IRequest<Result<int>>;

public sealed class ListDailyReportsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListDailyReportsQuery, Result<CursorPage<DailyReportDto>>>
{
    public async Task<Result<CursorPage<DailyReportDto>>> Handle(ListDailyReportsQuery request, CancellationToken ct)
    {
        var reports = db.DailyReports.AsNoTracking();
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            reports = reports.Where(r => own.Contains(r.StudentId));
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            reports = reports.Where(r => db.Students.Any(s => s.Id == r.StudentId && s.RoomId != null && rooms.Contains(s.RoomId.Value)));
        }

        if (request.StudentId is { } id)
        {
            reports = reports.Where(r => r.StudentId == id);
        }

        var page = await reports.ToCursorPageAsync(request.Cursor, request.Limit,
            r => new DailyReportDto(
                r.Id, r.StudentId, db.Students.Where(s => s.Id == r.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault() ?? "",
                r.Date, r.ActivityCount, r.CheckInAt, r.CheckOutAt, r.Summary, r.CreatedAt), ct);
        return Result<CursorPage<DailyReportDto>>.Success(page);
    }
}

public sealed class RunDailyReportsHandler(IAppDbContext db, CentreCalendar calendar, IOptions<DailyReportOptions> options, IClock clock)
    : IRequestHandler<RunDailyReportsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunDailyReportsCommand request, CancellationToken ct)
    {
        if (!TimeOnly.TryParseExact(options.Value.GenerateAt, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) || calendar.LocalTime < at)
        {
            return Result<int>.Success(0);
        }

        var today = calendar.Today;
        var present = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == today && a.PersonType == PersonTypes.Student && a.CheckInAt != null)
            .Select(a => new { a.PersonId, a.CheckInAt, a.CheckOutAt }).ToListAsync(ct);
        var done = (await db.DailyReports.AsNoTracking().Where(r => r.Date == today).Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();
        var todo = present.Where(p => !done.Contains(p.PersonId)).ToList();
        if (todo.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var ids = todo.Select(t => t.PersonId).ToList();
        var names = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FirstName, ct);
        var counts = await db.ActivityPostStudents.AsNoTracking()
            .Where(x => ids.Contains(x.StudentId) && db.ActivityPosts.Any(p => p.Id == x.PostId && p.ApprovalStatus == ApprovalStatuses.Approved && p.CreatedAt >= clock.UtcNow.AddHours(-24)))
            .GroupBy(x => x.StudentId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);

        foreach (var t in todo)
        {
            var name = names.GetValueOrDefault(t.PersonId, "");
            var n = counts.GetValueOrDefault(t.PersonId);
            string Local(DateTimeOffset? v) => v is null ? "" : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(v.Value, calendar.Zone).DateTime).ToString("HH:mm", CultureInfo.InvariantCulture);
            var stay = t.CheckOutAt is null ? $"{Local(t.CheckInAt)}" : $"{Local(t.CheckInAt)} - {Local(t.CheckOutAt)}";
            db.DailyReports.Add(new DailyReport
            {
                StudentId = t.PersonId, Date = today, GeneratedAt = clock.UtcNow, ActivityCount = n, CheckInAt = t.CheckInAt, CheckOutAt = t.CheckOutAt,
                Summary = new Dictionary<string, string>
                {
                    ["en"] = $"{name} was with us today ({stay}). {(n == 0 ? "No activity updates were posted." : $"{n} activity update(s) were shared with you.")}",
                    ["ar"] = $"كان {name} معنا اليوم ({stay}). {(n == 0 ? "لم تُنشر تحديثات للأنشطة." : $"تمت مشاركة {n} تحديث(ات) للأنشطة معكم.")}",
                },
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(todo.Count);
    }
}
