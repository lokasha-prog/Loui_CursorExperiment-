using System.Globalization;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Ai;

public static class BriefingAreas
{
    public const string Overview = "overview";
    public const string Finance = "finance";
    public const string Admissions = "admissions";
    public const string People = "people";

    public static readonly string[] All = [Overview, Finance, Admissions, People];
}

public sealed record BriefingFact(string Label, string Value);

public sealed record ManagementBriefingDto(string Area, string Summary, string Language, string Model, DateTimeOffset GeneratedAt, IReadOnlyList<BriefingFact> Facts);

public sealed record ManagementBriefingQuery(string Area, string Language) : IRequest<Result<ManagementBriefingDto>>;

public sealed class ManagementBriefingValidator : AbstractValidator<ManagementBriefingQuery>
{
    public ManagementBriefingValidator()
    {
        RuleFor(q => q.Area).Must(a => BriefingAreas.All.Contains(a)).WithMessage("Area must be overview, finance, admissions or people.");
        RuleFor(q => q.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
    }
}

/// <summary>
/// AI Brain briefings for the management team. The model is given totals only (counts, money, percentages, dates);
/// no child, parent or staff names and no free text ever leave the system. The same totals are returned so the manager can check the advice.
/// </summary>
public sealed class ManagementBriefingHandler(IAppDbContext db, CentreCalendar calendar, IClock clock, IAiBrain brain)
    : IRequestHandler<ManagementBriefingQuery, Result<ManagementBriefingDto>>
{
    private static string Aed(long fils) => (fils / 100m).ToString("N0", CultureInfo.InvariantCulture) + " AED";

    public async Task<Result<ManagementBriefingDto>> Handle(ManagementBriefingQuery request, CancellationToken ct)
    {
        if (!brain.IsConfigured)
        {
            return Error.InvalidState("The AI Brain is not set up yet.");
        }

        var facts = request.Area switch
        {
            BriefingAreas.Finance => await FinanceAsync(ct),
            BriefingAreas.Admissions => await AdmissionsAsync(ct),
            BriefingAreas.People => await PeopleAsync(ct),
            _ => await OverviewAsync(ct),
        };

        var user = new StringBuilder();
        user.AppendLine($"Today is {calendar.Today:yyyy-MM-dd} ({calendar.Today.DayOfWeek}). Figures for the centre:");
        foreach (var f in facts)
        {
            user.AppendLine($"- {f.Label}: {f.Value}");
        }

        var answer = await brain.CompleteAsync(SystemPrompt(request.Area, request.Language), user.ToString(), ct);
        if (answer.IsFailure)
        {
            return Result<ManagementBriefingDto>.Failure(answer.Errors);
        }

        return Result<ManagementBriefingDto>.Success(new ManagementBriefingDto(request.Area, answer.Value.Trim(), request.Language, brain.ModelName, clock.UtcNow, facts));
    }

    private async Task<List<BriefingFact>> OverviewAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var active = await db.Students.AsNoTracking().CountAsync(s => s.Status == StudentStatuses.Active, ct);
        var present = await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date == today && a.PersonType == PersonTypes.Student
            && (a.Status == AttendanceStatuses.Present || a.Status == AttendanceStatuses.Late || a.Status == AttendanceStatuses.EarlyLeave), ct);
        var staffPresent = await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date == today && a.PersonType == PersonTypes.Staff && a.Status != AttendanceStatuses.Absent && a.Status != AttendanceStatuses.Pending, ct);
        var shortages = await db.RatioBreaches.AsNoTracking().CountAsync(b => b.DetectedAt >= clock.UtcNow.AddDays(-30), ct);
        var pendingLeave = await db.LeaveRequests.AsNoTracking().CountAsync(l => l.Status == LeaveStatuses.Submitted, ct);
        var outstanding = await db.Invoices.AsNoTracking().Where(i => InvoiceStatuses.Open.Contains(i.Status)).SumAsync(i => (long?)(i.TotalFils - i.PaidFils), ct) ?? 0;
        var overdue = await db.Invoices.AsNoTracking().CountAsync(i => InvoiceStatuses.Open.Contains(i.Status) && i.DueDate < today, ct);
        var newEnquiries = await db.Enquiries.AsNoTracking().CountAsync(e => !e.IsArchived && e.EnquiryDate >= today.AddDays(-30), ct);
        var collected = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= monthStart).SumAsync(p => (long?)p.AmountFils, ct) ?? 0;
        var spent = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= monthStart).SumAsync(e => (long?)e.AmountFils, ct) ?? 0;
        return
        [
            new("Active children", active.ToString()),
            new("Children present today", present.ToString()),
            new("Staff present today", staffPresent.ToString()),
            new("Times a room was short of staff in the last 30 days", shortages.ToString()),
            new("Leave requests waiting for a decision", pendingLeave.ToString()),
            new("Money still to collect", Aed(outstanding)),
            new("Invoices past their due date", overdue.ToString()),
            new("Collected this month so far", Aed(collected)),
            new("Spent this month so far", Aed(spent)),
            new("New enquiries in the last 30 days", newEnquiries.ToString()),
        ];
    }

    private async Task<List<BriefingFact>> FinanceAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var billed = await db.Invoices.AsNoTracking().Where(i => i.IssueDate >= monthStart && i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled).SumAsync(i => (long?)i.TotalFils, ct) ?? 0;
        var collected = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= monthStart).SumAsync(p => (long?)p.AmountFils, ct) ?? 0;
        var open = await db.Invoices.AsNoTracking().Where(i => InvoiceStatuses.Open.Contains(i.Status)).Select(i => new { i.DueDate, Left = i.TotalFils - i.PaidFils }).ToListAsync(ct);
        long Bucket(int from, int to) => open.Where(i => (today.DayNumber - i.DueDate.DayNumber) >= from && (today.DayNumber - i.DueDate.DayNumber) <= to).Sum(i => i.Left);
        var notDue = open.Where(i => i.DueDate >= today).Sum(i => i.Left);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= monthStart).GroupBy(e => e.Category).Select(g => new { g.Key, Total = g.Sum(e => e.AmountFils) }).ToListAsync(ct);
        var spent = expenses.Sum(e => e.Total);
        var facts = new List<BriefingFact>
        {
            new("Billed this month", Aed(billed)),
            new("Collected this month so far", Aed(collected)),
            new("Collection rate this month", billed > 0 ? $"{Math.Round(collected * 100m / billed)}%" : "no invoices"),
            new("Not yet due", Aed(notDue)),
            new("Overdue 1-30 days", Aed(Bucket(1, 30))),
            new("Overdue 31-60 days", Aed(Bucket(31, 60))),
            new("Overdue over 60 days", Aed(Bucket(61, int.MaxValue))),
            new("Invoices overdue (count)", open.Count(i => i.DueDate < today).ToString()),
            new("Spent this month so far", Aed(spent)),
            new("Net this month (collected minus spent)", Aed(collected - spent)),
        };
        facts.AddRange(expenses.OrderByDescending(e => e.Total).Take(5).Select(e => new BriefingFact($"Spent on {e.Key}", Aed(e.Total))));
        return facts;
    }

    private async Task<List<BriefingFact>> AdmissionsAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var since = today.AddDays(-90);
        var recent = await db.Enquiries.AsNoTracking().Where(e => !e.IsArchived && e.EnquiryDate >= since).Select(e => new { e.Stage, e.Source, e.LastContactDate, e.EnquiryDate }).ToListAsync(ct);
        var facts = new List<BriefingFact> { new("Enquiries in the last 90 days", recent.Count.ToString()) };
        facts.AddRange(EnquiryStages.All.Select(stage => new BriefingFact($"At stage '{stage}'", recent.Count(e => e.Stage == stage).ToString())));
        var enrolled = recent.Count(e => e.Stage == EnquiryStages.Enrolled);
        facts.Add(new("Enquiry to enrolment rate", recent.Count > 0 ? $"{Math.Round(enrolled * 100m / recent.Count)}%" : "no enquiries"));
        facts.AddRange(recent.GroupBy(e => e.Source).OrderByDescending(g => g.Count()).Take(5).Select(g => new BriefingFact($"Enquiries from '{g.Key}'", $"{g.Count()} ({g.Count(e => e.Stage == EnquiryStages.Enrolled)} enrolled)")));
        var stale = recent.Count(e => e.Stage != EnquiryStages.Enrolled && (e.LastContactDate ?? e.EnquiryDate) < today.AddDays(-7));
        facts.Add(new("Open enquiries with no contact for over 7 days", stale.ToString()));
        var now = clock.UtcNow;
        var tours = await db.Tours.AsNoTracking().CountAsync(t => t.StartsAt >= now && t.StartsAt <= now.AddDays(7), ct);
        facts.Add(new("Tours booked in the next 7 days", tours.ToString()));
        var capacity = await db.Rooms.AsNoTracking().SumAsync(r => (int?)r.Capacity, ct) ?? 0;
        var active = await db.Students.AsNoTracking().CountAsync(s => s.Status == StudentStatuses.Active, ct);
        facts.Add(new("Places in use", capacity > 0 ? $"{active} of {capacity}" : $"{active} (no room capacity set)"));
        return facts;
    }

    private async Task<List<BriefingFact>> PeopleAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var staff = await db.Staff.AsNoTracking().CountAsync(s => s.Status == "active", ct);
        var present = await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date == today && a.PersonType == PersonTypes.Staff && a.Status != AttendanceStatuses.Absent && a.Status != AttendanceStatuses.Pending, ct);
        var late30 = await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date >= today.AddDays(-30) && a.PersonType == PersonTypes.Staff && a.Status == AttendanceStatuses.Late, ct);
        var absent30 = await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date >= today.AddDays(-30) && a.PersonType == PersonTypes.Staff && a.Status == AttendanceStatuses.Absent, ct);
        var shortages = await db.RatioBreaches.AsNoTracking().CountAsync(b => b.DetectedAt >= clock.UtcNow.AddDays(-30), ct);
        var pending = await db.LeaveRequests.AsNoTracking().CountAsync(l => l.Status == LeaveStatuses.Submitted && l.PersonType == PersonTypes.Staff, ct);
        var upcoming = await db.LeaveRequests.AsNoTracking().CountAsync(l => l.Status == LeaveStatuses.Approved && l.PersonType == PersonTypes.Staff && l.EndDate >= today && l.StartDate <= today.AddDays(14), ct);
        var children = await db.Students.AsNoTracking().CountAsync(s => s.Status == StudentStatuses.Active, ct);
        return
        [
            new("Active staff", staff.ToString()),
            new("Staff present today", present.ToString()),
            new("Active children", children.ToString()),
            new("Children per staff member (enrolled / active staff)", staff > 0 ? (children / (double)staff).ToString("0.0", CultureInfo.InvariantCulture) : "no staff"),
            new("Staff late arrivals in the last 30 days", late30.ToString()),
            new("Staff absences in the last 30 days", absent30.ToString()),
            new("Times a room was short of staff in the last 30 days", shortages.ToString()),
            new("Staff leave requests waiting for a decision", pending.ToString()),
            new("Staff leave approved in the next 14 days", upcoming.ToString()),
        ];
    }

    private static string SystemPrompt(string area, string language)
    {
        var focus = area switch
        {
            BriefingAreas.Finance => "money: collection rate, overdue ageing, spending against income, and which actions will bring in cash or cut cost",
            BriefingAreas.Admissions => "enrolment: the enquiry pipeline, where families drop out, the best sources, follow-ups that are overdue and how full the centre is",
            BriefingAreas.People => "staffing: cover, lateness, absence, leave pressure and whether the team is stretched",
            _ => "the whole centre today: attendance, staff cover, money, enquiries, and the most important things to act on this week",
        };
        return "You are the management assistant inside a nursery management system in the UAE. You receive only totals; you never see names or "
            + "personal details and must never guess them. Write a short briefing for the nursery's management team about " + focus + ". "
            + "Start with one line saying how things look overall, then up to three practical actions ordered by importance, each with the number that justifies it. "
            + "Use only the numbers given, never invent figures, and say plainly if the data is too thin to conclude. Keep it under 170 words. "
            + "Plain text only: no markdown, no asterisks, no headings, no tables; lines starting with a dash are fine. Answer in "
            + (language == "ar" ? "Arabic (Modern Standard Arabic, written right to left, Western digits)." : "English.");
    }
}
