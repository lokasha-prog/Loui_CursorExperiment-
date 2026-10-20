using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Billing;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed record MethodTotal(string Method, long TotalFils, int Count);

public sealed record RevenueReportDto(string Month, long BilledFils, long CollectedFils, long ExpensesFils, long NetFils, IReadOnlyList<MethodTotal> ByMethod, IReadOnlyList<MonthFigure> Trend);

public sealed record RevenueReportQuery(string? Month) : IRequest<Result<RevenueReportDto>>;

public sealed record OverdueRowDto(Guid InvoiceId, string InvoiceNumber, Guid StudentId, string? StudentName, string? Contact, DateOnly DueDate, int DaysLate, long OutstandingFils);

public sealed record OverdueReportDto(long TotalFils, IReadOnlyList<OverdueRowDto> Rows);

public sealed record OverdueReportQuery : IRequest<Result<OverdueReportDto>>;

/// <summary>Monthly revenue (spec 03 §8): billed, collected by payment method, expenses, net, and the six-month trend.</summary>
public sealed class RevenueReportHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<RevenueReportQuery, Result<RevenueReportDto>>
{
    public async Task<Result<RevenueReportDto>> Handle(RevenueReportQuery request, CancellationToken ct)
    {
        var (start, end, label) = ExpenseMonth.Range(request.Month, calendar.Today);
        var live = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled);
        var billed = await live.Where(i => i.IssueDate >= start && i.IssueDate <= end).SumAsync(i => (long?)i.TotalFils, ct) ?? 0;
        var payments = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= start && p.PaymentDate <= end)
            .GroupBy(p => p.Method).Select(g => new MethodTotal(g.Key, g.Sum(p => p.AmountFils), g.Count())).ToListAsync(ct);
        var collected = payments.Sum(p => p.TotalFils);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end).SumAsync(e => (long?)e.AmountFils, ct) ?? 0;

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

        return Result<RevenueReportDto>.Success(new RevenueReportDto(label, billed, collected, expenses, collected - expenses, payments.OrderByDescending(p => p.TotalFils).ToList(), trend));
    }
}

/// <summary>Every invoice past its due date and still unpaid, longest overdue first, with who to call.</summary>
public sealed class OverdueReportHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<OverdueReportQuery, Result<OverdueReportDto>>
{
    public async Task<Result<OverdueReportDto>> Handle(OverdueReportQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled && i.Status != InvoiceStatuses.Paid && i.DueDate < today && i.TotalFils > i.PaidFils)
            .OrderBy(i => i.DueDate)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.StudentId, i.DueDate, Owed = i.TotalFils - i.PaidFils,
                Name = db.Students.Where(s => s.Id == i.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
                Contact = db.StudentGuardians.Where(g => g.StudentId == i.StudentId && g.Guardian!.IsPrimaryContact).Select(g => g.Guardian!.Phone).FirstOrDefault(),
            }).ToListAsync(ct);
        var list = rows.Select(r => new OverdueRowDto(r.Id, r.InvoiceNumber, r.StudentId, r.Name, r.Contact, r.DueDate, today.DayNumber - r.DueDate.DayNumber, r.Owed)).ToList();
        return Result<OverdueReportDto>.Success(new OverdueReportDto(list.Sum(r => r.OutstandingFils), list));
    }
}
