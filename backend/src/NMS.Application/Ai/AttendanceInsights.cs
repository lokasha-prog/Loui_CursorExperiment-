using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Attendance;
using NMS.Shared.Results;

namespace NMS.Application.Ai;

/// <summary>
/// The AI Brain's single door to a language model. Callers pass only text that already contains no personal data;
/// the provider (Groq today) is chosen by configuration and can be replaced without touching callers.
/// </summary>
public interface IAiBrain
{
    bool IsConfigured { get; }

    string ModelName { get; }

    Task<Result<string>> CompleteAsync(string system, string user, CancellationToken cancellationToken);
}

public sealed record DailyAttendanceFigure(DateOnly Date, int StudentsPresent, int StudentsLate, int StudentsLeftEarly, int StudentsAbsent, int StaffPresent);

public sealed record AttendanceInsightsDto(
    string Summary,
    int Days,
    string Language,
    string Model,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DailyAttendanceFigure> Figures,
    int ShortageEvents,
    int ApprovedLeaveRequests);

public sealed record AttendanceInsightsQuery(int Days, string Language) : IRequest<Result<AttendanceInsightsDto>>;

public sealed class AttendanceInsightsValidator : AbstractValidator<AttendanceInsightsQuery>
{
    public AttendanceInsightsValidator()
    {
        RuleFor(q => q.Days).InclusiveBetween(3, 60);
        RuleFor(q => q.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
    }
}

public sealed class AttendanceInsightsHandler(IAppDbContext db, CentreCalendar calendar, IClock clock, IAiBrain brain)
    : IRequestHandler<AttendanceInsightsQuery, Result<AttendanceInsightsDto>>
{
    public async Task<Result<AttendanceInsightsDto>> Handle(AttendanceInsightsQuery request, CancellationToken ct)
    {
        if (!brain.IsConfigured)
        {
            return Error.InvalidState("The AI Brain is not set up yet.");
        }

        var to = calendar.Today;
        var from = to.AddDays(-(request.Days - 1));

        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to)
            .GroupBy(a => new { a.Date, a.PersonType, a.Status })
            .Select(g => new { g.Key.Date, g.Key.PersonType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var shortages = await db.RatioBreaches.AsNoTracking().CountAsync(b => b.DetectedAt >= clock.UtcNow.AddDays(-request.Days), ct);
        var leaves = await db.LeaveRequests.AsNoTracking()
            .CountAsync(l => l.Status == LeaveStatuses.Approved && l.EndDate >= from && l.StartDate <= to, ct);

        var figures = Enumerable.Range(0, request.Days).Select(i => from.AddDays(i)).Select(day =>
        {
            int Count(string type, params string[] statuses) =>
                rows.Where(r => r.Date == day && r.PersonType == type && statuses.Contains(r.Status)).Sum(r => r.Count);
            return new DailyAttendanceFigure(
                day,
                Count(PersonTypes.Student, AttendanceStatuses.Present, AttendanceStatuses.Late, AttendanceStatuses.EarlyLeave),
                Count(PersonTypes.Student, AttendanceStatuses.Late),
                Count(PersonTypes.Student, AttendanceStatuses.EarlyLeave),
                Count(PersonTypes.Student, AttendanceStatuses.Absent),
                Count(PersonTypes.Staff, AttendanceStatuses.Present, AttendanceStatuses.Late, AttendanceStatuses.EarlyLeave));
        }).ToList();

        var answer = await brain.CompleteAsync(SystemPrompt(request.Language), UserPrompt(figures, shortages, leaves, request.Days), ct);
        if (answer.IsFailure)
        {
            return Result<AttendanceInsightsDto>.Failure(answer.Errors);
        }

        return Result<AttendanceInsightsDto>.Success(new AttendanceInsightsDto(
            answer.Value.Trim(), request.Days, request.Language, brain.ModelName, clock.UtcNow, figures, shortages, leaves));
    }

    private static string SystemPrompt(string language) =>
        "You are the analytics assistant inside a nursery management system in the UAE. You are given only daily totals; "
        + "you never see names or personal details and must never guess them. Write a short, plain-language summary for the "
        + "nursery manager: overall attendance trend, notable days, patterns by weekday, lateness and absence, staff cover, and "
        + "one or two practical suggestions. Use only the numbers given and say so if the data is too thin to conclude anything. "
        + "Keep it under 180 words. Plain text only: no markdown, no asterisks, no headings, no tables; use short paragraphs or lines starting with a dash. Answer in "
        + (language == "ar" ? "Arabic (Modern Standard Arabic, written right to left, Western digits)." : "English.");

    private static string UserPrompt(IReadOnlyList<DailyAttendanceFigure> figures, int shortages, int leaves, int days)
    {
        var text = new StringBuilder();
        text.AppendLine($"Period: last {days} days. Columns: date (weekday), children present, late, left early, absent, staff present.");
        foreach (var f in figures)
        {
            text.AppendLine($"{f.Date:yyyy-MM-dd} ({f.Date.DayOfWeek}): {f.StudentsPresent}, {f.StudentsLate}, {f.StudentsLeftEarly}, {f.StudentsAbsent}, {f.StaffPresent}");
        }

        text.AppendLine($"Times a room was short of staff in the period: {shortages}.");
        text.AppendLine($"Approved leave requests overlapping the period: {leaves}.");
        return text.ToString();
    }
}
