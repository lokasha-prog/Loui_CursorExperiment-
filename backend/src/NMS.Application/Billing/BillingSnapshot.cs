using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Billing;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed record MonthFigure(string Month, long BilledFils, long CollectedFils);

public sealed record RecentPaymentDto(Guid InvoiceId, string InvoiceNumber, string? StudentName, long AmountFils, DateOnly PaymentDate, string Method);

public sealed record TopOverdueDto(Guid StudentId, string? StudentName, long OutstandingFils, int Invoices);

public sealed record BillingSnapshotDto(
    string Month,
    long BilledFils,
    long CollectedFils,
    long OverdueFils,
    long OutstandingFils,
    int NewInvoicesToday,
    long NewInvoicesTodayFils,
    IReadOnlyList<RecentPaymentDto> RecentPayments,
    IReadOnlyList<TopOverdueDto> TopOverdue,
    IReadOnlyList<MonthFigure> Trend);

public sealed record BillingSnapshotQuery(string? Month) : IRequest<Result<BillingSnapshotDto>>;

/// <summary>The numbers on the billing dashboard (spec 03 §8.1): what was billed and collected this month, what is owed, and the trend.</summary>
public sealed class BillingSnapshotHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<BillingSnapshotQuery, Result<BillingSnapshotDto>>
{
    public async Task<Result<BillingSnapshotDto>> Handle(BillingSnapshotQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var start = request.Month is { } m && DateOnly.TryParseExact(m + "-01", "yyyy-MM-dd", out var parsed) ? parsed : new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var live = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled);

        var billed = await live.Where(i => i.IssueDate >= start && i.IssueDate <= end).SumAsync(i => (long?)i.TotalFils, ct) ?? 0;
        var collected = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= start && p.PaymentDate <= end).SumAsync(p => (long?)p.AmountFils, ct) ?? 0;
        var overdue = await live.Where(i => i.Status == InvoiceStatuses.Overdue).SumAsync(i => (long?)(i.TotalFils - i.PaidFils), ct) ?? 0;
        var outstanding = await live.Where(i => i.Status == InvoiceStatuses.Issued || i.Status == InvoiceStatuses.PartiallyPaid)
            .SumAsync(i => (long?)(i.TotalFils - i.PaidFils), ct) ?? 0;
        var todays = await live.Where(i => i.IssueDate == today).Select(i => i.TotalFils).ToListAsync(ct);

        var recent = await db.InvoicePayments.AsNoTracking().OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt).Take(10)
            .Select(p => new RecentPaymentDto(
                p.InvoiceId, db.Invoices.Where(i => i.Id == p.InvoiceId).Select(i => i.InvoiceNumber).First(),
                db.Invoices.Where(i => i.Id == p.InvoiceId).Select(i => db.Students.Where(s => s.Id == i.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault()).First(),
                p.AmountFils, p.PaymentDate, p.Method))
            .ToListAsync(ct);

        var late = await live.Where(i => i.Status == InvoiceStatuses.Overdue).GroupBy(i => i.StudentId)
            .Select(g => new { StudentId = g.Key, Owed = g.Sum(i => i.TotalFils - i.PaidFils), Count = g.Count() })
            .OrderByDescending(g => g.Owed).Take(5).ToListAsync(ct);
        var ids = late.Select(l => l.StudentId).ToList();
        var names = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FirstName + " " + s.LastName, ct);

        var trend = new List<MonthFigure>();
        for (var i = 5; i >= 0; i--)
        {
            var from = start.AddMonths(-i);
            var to = from.AddMonths(1).AddDays(-1);
            trend.Add(new MonthFigure(
                from.ToString("yyyy-MM"),
                await live.Where(x => x.IssueDate >= from && x.IssueDate <= to).SumAsync(x => (long?)x.TotalFils, ct) ?? 0,
                await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= from && p.PaymentDate <= to).SumAsync(p => (long?)p.AmountFils, ct) ?? 0));
        }

        return Result<BillingSnapshotDto>.Success(new BillingSnapshotDto(
            start.ToString("yyyy-MM"), billed, collected, overdue, outstanding, todays.Count, todays.Sum(), recent,
            late.Select(l => new TopOverdueDto(l.StudentId, names.GetValueOrDefault(l.StudentId), l.Owed, l.Count)).ToList(), trend));
    }
}
