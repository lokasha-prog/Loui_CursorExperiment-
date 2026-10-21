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
    long ExpensesFils,
    long NetFils,
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

        // Money figures are added up in the database in as few queries as possible; this screen is opened by every manager all day.
        var open = await live.GroupBy(_ => 1).Select(g => new
        {
            Overdue = g.Sum(i => i.Status == InvoiceStatuses.Overdue ? i.TotalFils - i.PaidFils : 0L),
            Outstanding = g.Sum(i => i.Status == InvoiceStatuses.Issued || i.Status == InvoiceStatuses.PartiallyPaid ? i.TotalFils - i.PaidFils : 0L),
        }).FirstOrDefaultAsync(ct);
        var overdue = open?.Overdue ?? 0;
        var outstanding = open?.Outstanding ?? 0;
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end).SumAsync(e => (long?)e.AmountFils, ct) ?? 0;
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

        var trendStart = start.AddMonths(-5);
        var billedByMonth = (await live.Where(x => x.IssueDate >= trendStart && x.IssueDate <= end)
            .GroupBy(x => new { x.IssueDate.Year, x.IssueDate.Month }).Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.TotalFils) }).ToListAsync(ct))
            .ToDictionary(x => (x.Year, x.Month), x => x.Total);
        var collectedByMonth = (await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= trendStart && p.PaymentDate <= end)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month }).Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.AmountFils) }).ToListAsync(ct))
            .ToDictionary(x => (x.Year, x.Month), x => x.Total);
        var trend = Enumerable.Range(0, 6).Select(n => trendStart.AddMonths(n))
            .Select(d => new MonthFigure(d.ToString("yyyy-MM"), billedByMonth.GetValueOrDefault((d.Year, d.Month)), collectedByMonth.GetValueOrDefault((d.Year, d.Month)))).ToList();
        var billed = billedByMonth.GetValueOrDefault((start.Year, start.Month));
        var collected = collectedByMonth.GetValueOrDefault((start.Year, start.Month));

        return Result<BillingSnapshotDto>.Success(new BillingSnapshotDto(
            start.ToString("yyyy-MM"), billed, collected, overdue, outstanding, expenses, collected - expenses, todays.Count, todays.Sum(), recent,
            late.Select(l => new TopOverdueDto(l.StudentId, names.GetValueOrDefault(l.StudentId), l.Owed, l.Count)).ToList(), trend));
    }
}
