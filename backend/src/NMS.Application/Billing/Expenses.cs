using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Domain.Billing;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed record ExpenseDto(Guid Id, DateOnly ExpenseDate, long AmountFils, string Category, string Description, Guid? ReceiptMediaId, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListExpensesQuery(string? Month, string? Category, string? Cursor, int? Limit) : IRequest<Result<CursorPage<ExpenseDto>>>;

public sealed record SaveExpenseCommand(Guid? Id, DateOnly ExpenseDate, long AmountFils, string Category, string Description, Guid? ReceiptMediaId) : IRequest<Result<ExpenseDto>>;

public sealed record DeleteExpenseCommand(Guid Id) : IRequest<Result>;

public sealed record CategoryTotal(string Category, long TotalFils, int Count);

public sealed record ExpenseReportDto(string Month, long TotalFils, IReadOnlyList<CategoryTotal> ByCategory, IReadOnlyList<ExpenseDto> Items);

public sealed record ExpenseReportQuery(string? Month) : IRequest<Result<ExpenseReportDto>>;

public sealed class SaveExpenseValidator : AbstractValidator<SaveExpenseCommand>
{
    public SaveExpenseValidator()
    {
        RuleFor(x => x.AmountFils).InclusiveBetween(1, 1_000_000_000);
        RuleFor(x => x.Category).Must(c => ExpenseCategories.All.Contains(c)).WithMessage("Choose one of the listed categories.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

internal static class ExpenseMonth
{
    public static (DateOnly Start, DateOnly End, string Label) Range(string? month, DateOnly today)
    {
        var start = month is not null && DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out var parsed) ? parsed : new DateOnly(today.Year, today.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1), start.ToString("yyyy-MM"));
    }

    public static IQueryable<ExpenseDto> Project(IQueryable<Expense> q) =>
        q.Select(e => new ExpenseDto(e.Id, e.ExpenseDate, e.AmountFils, e.Category, e.Description, e.ReceiptMediaId, e.CreatedAt));
}

public sealed class ListExpensesHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<ListExpensesQuery, Result<CursorPage<ExpenseDto>>>
{
    public async Task<Result<CursorPage<ExpenseDto>>> Handle(ListExpensesQuery request, CancellationToken ct)
    {
        var query = db.Expenses.AsNoTracking();
        if (request.Month is not null)
        {
            var (start, end, _) = ExpenseMonth.Range(request.Month, calendar.Today);
            query = query.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end);
        }

        if (request.Category is not null)
        {
            query = query.Where(e => e.Category == request.Category);
        }

        return Result<CursorPage<ExpenseDto>>.Success(await query.ToCursorPageAsync(request.Cursor, request.Limit,
            e => new ExpenseDto(e.Id, e.ExpenseDate, e.AmountFils, e.Category, e.Description, e.ReceiptMediaId, e.CreatedAt), ct));
    }
}

public sealed class SaveExpenseHandler(IAppDbContext db, ICurrentUser current, CentreCalendar calendar) : IRequestHandler<SaveExpenseCommand, Result<ExpenseDto>>
{
    public async Task<Result<ExpenseDto>> Handle(SaveExpenseCommand request, CancellationToken ct)
    {
        if (request.ExpenseDate > calendar.Today)
        {
            return Error.Validation("The expense date cannot be in the future.", "expense_date");
        }

        if (request.ReceiptMediaId is { } media && !await db.MediaFiles.AnyAsync(m => m.Id == media, ct))
        {
            return Error.Validation("The receipt file does not exist.", "receipt_media_id");
        }

        Expense expense;
        if (request.Id is { } id)
        {
            var found = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Expense not found.");
            }

            expense = found;
        }
        else
        {
            expense = new Expense { RecordedBy = current.UserId };
            db.Expenses.Add(expense);
        }

        expense.ExpenseDate = request.ExpenseDate;
        expense.AmountFils = request.AmountFils;
        expense.Category = request.Category;
        expense.Description = request.Description.Trim();
        expense.ReceiptMediaId = request.ReceiptMediaId;
        await db.SaveChangesAsync(ct);
        return Result<ExpenseDto>.Success(new ExpenseDto(expense.Id, expense.ExpenseDate, expense.AmountFils, expense.Category, expense.Description, expense.ReceiptMediaId, expense.CreatedAt));
    }
}

public sealed class DeleteExpenseHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteExpenseCommand, Result>
{
    public async Task<Result> Handle(DeleteExpenseCommand request, CancellationToken ct)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (expense is null)
        {
            return Result.Failure(Error.NotFound("Expense not found."));
        }

        expense.IsDeleted = true;
        expense.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>The monthly expense report (spec 03 §8.5): total, split by category, and every line.</summary>
public sealed class ExpenseReportHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<ExpenseReportQuery, Result<ExpenseReportDto>>
{
    public async Task<Result<ExpenseReportDto>> Handle(ExpenseReportQuery request, CancellationToken ct)
    {
        var (start, end, label) = ExpenseMonth.Range(request.Month, calendar.Today);
        var month = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end);
        var items = await ExpenseMonth.Project(month.OrderBy(e => e.ExpenseDate).ThenBy(e => e.CreatedAt)).ToListAsync(ct);
        var groups = items.GroupBy(i => i.Category).Select(g => new CategoryTotal(g.Key, g.Sum(i => i.AmountFils), g.Count()))
            .OrderByDescending(g => g.TotalFils).ToList();
        return Result<ExpenseReportDto>.Success(new ExpenseReportDto(label, items.Sum(i => i.AmountFils), groups, items));
    }
}
