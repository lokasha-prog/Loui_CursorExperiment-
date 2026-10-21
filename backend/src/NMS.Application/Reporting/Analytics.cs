using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Security;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Billing;
using NMS.Shared.Results;

namespace NMS.Application.Reporting;

public sealed record TrendPoint(string Label, double Value);

public sealed record MoneyPoint(string Month, long BilledFils, long CollectedFils, long ExpensesFils);

public sealed record CountPoint(string Label, int Count);

public sealed record AnalyticsDto(
    DateOnly From, DateOnly To, IReadOnlyList<TrendPoint> AttendanceRate, IReadOnlyList<CountPoint> AttendanceByStatus,
    IReadOnlyList<MoneyPoint>? Revenue, IReadOnlyList<CountPoint>? InvoiceStatus,
    IReadOnlyList<CountPoint>? EnquiriesByMonth, IReadOnlyList<CountPoint>? EnrolledByMonth, IReadOnlyList<CountPoint>? EnquiriesBySource);

public sealed record AnalyticsQuery(DateOnly From, DateOnly To) : IRequest<Result<AnalyticsDto>>, NMS.Application.Common.IShortCachedQuery
{
    public string CacheKey => $"{From:O}|{To:O}";
}


public sealed class AnalyticsValidator : AbstractValidator<AnalyticsQuery>
{
    public AnalyticsValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 366).WithMessage("Choose a range of at most a year.");
    }
}

/// <summary>The numbers behind the analytics charts. Money and enquiry figures are included only for people allowed to see them.</summary>
public sealed class AnalyticsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<AnalyticsQuery, Result<AnalyticsDto>>
{
    public async Task<Result<AnalyticsDto>> Handle(AnalyticsQuery request, CancellationToken ct)
    {
        var (from, to) = (request.From, request.To);
        var days = to.DayNumber - from.DayNumber + 1;
        var records = await db.AttendanceRecords.AsNoTracking().Where(a => a.PersonType == PersonTypes.Student && a.Date >= from && a.Date <= to)
            .Select(a => new { a.Date, a.Status }).ToListAsync(ct);
        string Bucket(DateOnly d) => days <= 62 ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM");
        bool Attended(string s) => s is AttendanceStatuses.Present or AttendanceStatuses.Late or AttendanceStatuses.EarlyLeave;
        var rate = records.Where(r => r.Status != AttendanceStatuses.Pending).GroupBy(r => Bucket(r.Date)).OrderBy(g => g.Key)
            .Select(g => new TrendPoint(g.Key, Math.Round(g.Count(r => Attended(r.Status)) * 100.0 / g.Count(), 1))).ToList();
        var byStatus = records.GroupBy(r => r.Status).OrderByDescending(g => g.Count()).Select(g => new CountPoint(g.Key, g.Count())).ToList();

        IReadOnlyList<MoneyPoint>? revenue = null;
        IReadOnlyList<CountPoint>? invoiceStatus = null;
        if (current.HasPermission(Permissions.BillingRead))
        {
            var months = MonthsBetween(from, to);
            var points = new List<MoneyPoint>();
            foreach (var m in months)
            {
                var start = m;
                var end = m.AddMonths(1).AddDays(-1);
                var billed = await db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled && i.IssueDate >= start && i.IssueDate <= end).SumAsync(i => (long?)i.TotalFils, ct) ?? 0;
                var collected = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= start && p.PaymentDate <= end).SumAsync(p => (long?)p.AmountFils, ct) ?? 0;
                var spent = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end).SumAsync(e => (long?)e.AmountFils, ct) ?? 0;
                points.Add(new MoneyPoint(m.ToString("yyyy-MM"), billed, collected, spent));
            }

            revenue = points;
            invoiceStatus = await db.Invoices.AsNoTracking().Where(i => i.IssueDate >= from && i.IssueDate <= to && i.Status != InvoiceStatuses.Draft).GroupBy(i => i.Status)
                .Select(g => new CountPoint(g.Key, g.Count())).ToListAsync(ct);
        }

        IReadOnlyList<CountPoint>? enquiries = null;
        IReadOnlyList<CountPoint>? enrolled = null;
        IReadOnlyList<CountPoint>? bySource = null;
        if (current.HasPermission(Permissions.EnquiriesRead))
        {
            var list = await db.Enquiries.AsNoTracking().Where(e => e.EnquiryDate >= from && e.EnquiryDate <= to).Select(e => new { e.EnquiryDate, e.Stage, e.Source }).ToListAsync(ct);
            var months = MonthsBetween(from, to);
            enquiries = months.Select(m => new CountPoint(m.ToString("yyyy-MM"), list.Count(e => e.EnquiryDate.Year == m.Year && e.EnquiryDate.Month == m.Month))).ToList();
            enrolled = months.Select(m => new CountPoint(m.ToString("yyyy-MM"), list.Count(e => e.Stage == EnquiryStages.Enrolled && e.EnquiryDate.Year == m.Year && e.EnquiryDate.Month == m.Month))).ToList();
            bySource = list.GroupBy(e => e.Source).OrderByDescending(g => g.Count()).Select(g => new CountPoint(g.Key, g.Count())).ToList();
        }

        return Result<AnalyticsDto>.Success(new AnalyticsDto(from, to, rate, byStatus, revenue, invoiceStatus, enquiries, enrolled, bySource));
    }

    private static List<DateOnly> MonthsBetween(DateOnly from, DateOnly to)
    {
        var list = new List<DateOnly>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            list.Add(m);
        }

        return list;
    }
}
