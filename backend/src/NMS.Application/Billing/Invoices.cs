using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed class BillingOptions
{
    public const string Section = "Billing";

    /// <summary>Days between the issue date and the due date when nothing else is chosen.</summary>
    public int DefaultDueDays { get; set; } = 14;
}

public sealed record InvoiceDto(
    Guid Id, string InvoiceNumber, Guid StudentId, string? StudentName, DateOnly IssueDate, DateOnly DueDate, DateOnly PeriodStart, DateOnly PeriodEnd,
    long TotalFils, long PaidFils, string Status, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record InvoiceLineDto(Guid Id, Dictionary<string, string> Description, decimal Quantity, long UnitPriceFils, long DiscountFils, long TaxFils, long TotalFils);

public sealed record PaymentDto(Guid Id, long AmountFils, DateOnly PaymentDate, string Method, string? Reference, string? Notes, string? RecordedByName, DateTimeOffset CreatedAt);

public sealed record InvoiceDetailDto(
    InvoiceDto Invoice, Guid? FeePlanId, long SubtotalFils, long DiscountFils, long TaxFils, Dictionary<string, string>? Notes, DateTimeOffset? SentAt,
    IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<PaymentDto> Payments);

public sealed record ListInvoicesQuery(string? Status, Guid? StudentId, string? Cursor, int? Limit) : IRequest<Result<CursorPage<InvoiceDto>>>;

public sealed record GetInvoiceQuery(Guid Id) : IRequest<Result<InvoiceDetailDto>>;

public sealed record LineInput(Dictionary<string, string> Description, decimal Quantity, long UnitPriceFils, decimal? TaxRatePercent, long DiscountFils = 0);

public sealed record CreateInvoiceCommand(
    Guid StudentId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly? IssueDate, DateOnly? DueDate, IReadOnlyList<LineInput> Lines,
    Dictionary<string, string>? Notes, bool Issue) : IRequest<Result<InvoiceDetailDto>>;

public sealed record IssueInvoiceCommand(Guid Id) : IRequest<Result<InvoiceDetailDto>>;

public sealed record CancelInvoiceCommand(Guid Id) : IRequest<Result<InvoiceDetailDto>>;

public sealed record SendInvoiceCommand(Guid Id) : IRequest<Result<InvoiceDetailDto>>;

public sealed record RecordPaymentCommand(Guid InvoiceId, long AmountFils, DateOnly PaymentDate, string Method, string? Reference, string? Notes)
    : IRequest<Result<InvoiceDetailDto>>;

/// <summary>Creates the invoices that are due for a month from the fee plans children are on. Safe to repeat: nothing is invoiced twice.</summary>
public sealed record GenerateInvoicesCommand(string? Month) : IRequest<Result<int>>;

/// <summary>Scheduled job, per centre: raises the current month's invoices and marks late ones overdue.</summary>
public sealed record RunBillingCommand : IRequest<Result<int>>;

public sealed record InvoiceIssued(Guid TenantId, Guid InvoiceId) : INotification;

public sealed record PaymentRecorded(Guid TenantId, Guid InvoiceId, long AmountFils) : INotification;

public sealed record InvoicePdfQuery(Guid Id, string Language) : IRequest<Result<InvoicePdfFile>>;

public sealed record InvoicePdfFile(byte[] Pdf, string FileName);

public sealed record InvoicePdfData(
    string CentreName, string InvoiceNumber, string StudentName, IReadOnlyList<string> Guardians, DateOnly IssueDate, DateOnly DueDate, DateOnly PeriodStart,
    DateOnly PeriodEnd, string Status, string Language, IReadOnlyList<InvoicePdfLine> Lines, long SubtotalFils, long DiscountFils, long TaxFils, long TotalFils,
    long PaidFils, string? Notes);

public sealed record InvoicePdfLine(string Description, decimal Quantity, long UnitPriceFils, long DiscountFils, long TaxFils, long TotalFils);

/// <summary>Draws the invoice. Implemented outside this layer so no PDF library leaks in here.</summary>
public interface IInvoiceRenderer
{
    byte[] Render(InvoicePdfData data);
}

public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(c => c.PeriodEnd).GreaterThanOrEqualTo(c => c.PeriodStart).WithMessage("The period end cannot be before its start.");
        RuleFor(c => c.Lines).NotEmpty().WithMessage("Add at least one line.");
        RuleForEach(c => c.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).MustHaveEnglish();
            l.RuleFor(x => x.Quantity).InclusiveBetween(0.01m, 100000m);
            l.RuleFor(x => x.UnitPriceFils).InclusiveBetween(0, 1_000_000_000);
            l.RuleFor(x => x.DiscountFils).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRatePercent!.Value).InclusiveBetween(0m, 100m).When(x => x.TaxRatePercent is not null);
        });
        RuleFor(c => c.DueDate!.Value).GreaterThanOrEqualTo(c => c.IssueDate ?? c.PeriodStart).When(c => c.DueDate is not null)
            .WithMessage("The due date cannot be before the issue date.");
    }
}

public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(c => c.AmountFils).GreaterThan(0);
        RuleFor(c => c.Method).Must(m => PaymentMethods.All.Contains(m)).WithMessage("Unknown payment method.");
        RuleFor(c => c.Reference!).MaximumLength(100).When(c => c.Reference is not null);
        RuleFor(c => c.Notes!).MaximumLength(500).When(c => c.Notes is not null);
    }
}

public sealed class GenerateInvoicesValidator : AbstractValidator<GenerateInvoicesCommand>
{
    public GenerateInvoicesValidator() =>
        RuleFor(c => c.Month!).Must(m => DateOnly.TryParseExact(m + "-01", "yyyy-MM-dd", out _)).When(c => c.Month is not null)
            .WithMessage("Month must look like 2026-10.");
}

internal static class InvoiceScope
{
    /// <summary>Families see only issued invoices of their own children; staff with billing access see everything.</summary>
    public static async Task<IQueryable<Invoice>> ScopeAsync(IQueryable<Invoice> invoices, IAccessScope scope, CancellationToken ct)
    {
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            invoices = invoices.Where(i => own.Contains(i.StudentId) && i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled);
        }

        return invoices;
    }

    public static InvoiceDto ToDto(Invoice i, string? studentName) => new(
        i.Id, i.InvoiceNumber, i.StudentId, studentName, i.IssueDate, i.DueDate, i.PeriodStart, i.PeriodEnd, i.TotalFils, i.PaidFils, i.Status, i.CreatedAt);

    public static async Task<InvoiceDetailDto> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Lines).Include(i => i.Payments).FirstAsync(i => i.Id == id, ct);
        var name = await db.Students.AsNoTracking().Where(s => s.Id == invoice.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct);
        var recorders = invoice.Payments.Select(p => p.RecordedBy).Distinct().ToList();
        var names = await db.AppUsers.AsNoTracking().Where(u => recorders.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return new InvoiceDetailDto(
            ToDto(invoice, name), invoice.FeePlanId, invoice.SubtotalFils, invoice.DiscountFils, invoice.TaxFils, invoice.Notes, invoice.SentAt,
            invoice.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(l => new InvoiceLineDto(l.Id, l.Description, l.Quantity, l.UnitPriceFils, l.DiscountFils, l.TaxFils, l.TotalFils)).ToList(),
            invoice.Payments.OrderBy(p => p.PaymentDate).ThenBy(p => p.CreatedAt)
                .Select(p => new PaymentDto(p.Id, p.AmountFils, p.PaymentDate, p.Method, p.Reference, p.Notes, names.GetValueOrDefault(p.RecordedBy), p.CreatedAt)).ToList());
    }

    /// <summary>Numbers run INV-2026-00001, 00002 and so on, per centre and year, counting removed invoices so a number is never reused.</summary>
    public static async Task<int> LastNumberAsync(IAppDbContext db, int year, CancellationToken ct)
    {
        var prefix = $"INV-{year}-";
        var numbers = await db.Invoices.IgnoreQueryFilters(["SoftDelete"]).AsNoTracking()
            .Where(i => i.InvoiceNumber.StartsWith(prefix)).Select(i => i.InvoiceNumber).ToListAsync(ct);
        return numbers.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
    }

    public static string Number(int year, int sequence) => $"INV-{year}-{sequence:D5}";

    public static string StatusAfterPayment(Invoice invoice, DateOnly today) =>
        invoice.PaidFils >= invoice.TotalFils ? InvoiceStatuses.Paid
        : invoice.DueDate < today ? InvoiceStatuses.Overdue
        : invoice.PaidFils > 0 ? InvoiceStatuses.PartiallyPaid : InvoiceStatuses.Issued;
}

public sealed class ListInvoicesHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListInvoicesQuery, Result<CursorPage<InvoiceDto>>>
{
    public async Task<Result<CursorPage<InvoiceDto>>> Handle(ListInvoicesQuery request, CancellationToken ct)
    {
        var invoices = await InvoiceScope.ScopeAsync(db.Invoices.AsNoTracking(), scope, ct);
        if (request.Status is { } status)
        {
            invoices = invoices.Where(i => i.Status == status);
        }

        if (request.StudentId is { } studentId)
        {
            invoices = invoices.Where(i => i.StudentId == studentId);
        }

        var page = await invoices.ToCursorPageAsync(request.Cursor, request.Limit,
            i => new InvoiceDto(
                i.Id, i.InvoiceNumber, i.StudentId, db.Students.Where(s => s.Id == i.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
                i.IssueDate, i.DueDate, i.PeriodStart, i.PeriodEnd, i.TotalFils, i.PaidFils, i.Status, i.CreatedAt), ct);
        return Result<CursorPage<InvoiceDto>>.Success(page);
    }
}

public sealed class GetInvoiceHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetInvoiceQuery, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(GetInvoiceQuery request, CancellationToken ct) =>
        await (await InvoiceScope.ScopeAsync(db.Invoices.AsNoTracking(), scope, ct)).AnyAsync(i => i.Id == request.Id, ct)
            ? Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, request.Id, ct))
            : Error.NotFound("Invoice not found.");
}

public sealed class CreateInvoiceHandler(IAppDbContext db, CentreCalendar calendar, ITenantContext tenant, BillingSettingsReader settings, IPublisher publisher)
    : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId, ct))
        {
            return Error.Validation("The child does not exist.", "student_id");
        }

        var today = calendar.Today;
        var dueDays = (await settings.GetAsync(ct)).DueDays;
        var issue = request.IssueDate ?? today;
        var invoice = new Invoice
        {
            StudentId = request.StudentId, IssueDate = issue, DueDate = request.DueDate ?? issue.AddDays(dueDays),
            PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, Notes = request.Notes,
            Status = request.Issue ? InvoiceStatuses.Issued : InvoiceStatuses.Draft,
        };
        var order = 0;
        foreach (var line in request.Lines)
        {
            var gross = InvoiceMath.Round(line.Quantity * line.UnitPriceFils);
            invoice.Lines.Add(new InvoiceLine
            {
                Description = line.Description, Quantity = line.Quantity, UnitPriceFils = line.UnitPriceFils, DiscountFils = Math.Min(line.DiscountFils, gross),
                TaxFils = InvoiceMath.TaxOn(gross - Math.Min(line.DiscountFils, gross), line.TaxRatePercent), SortOrder = order++,
            });
        }

        InvoiceMath.Recalculate(invoice, 0);
        invoice.Number(await InvoiceScope.LastNumberAsync(db, issue.Year, ct) + 1);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        if (request.Issue)
        {
            await publisher.Publish(new InvoiceIssued(tenant.TenantId, invoice.Id), ct);
        }

        return Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, invoice.Id, ct));
    }
}

internal static class InvoiceNumbering
{
    public static void Number(this Invoice invoice, int sequence) => invoice.InvoiceNumber = InvoiceScope.Number(invoice.IssueDate.Year, sequence);
}

public sealed class IssueInvoiceHandler(IAppDbContext db, ITenantContext tenant, IPublisher publisher) : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(IssueInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        if (invoice.Status != InvoiceStatuses.Draft)
        {
            return Error.InvalidState("Only a draft invoice can be issued.");
        }

        invoice.Status = InvoiceStatuses.Issued;
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new InvoiceIssued(tenant.TenantId, invoice.Id), ct);
        return Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, invoice.Id, ct));
    }
}

public sealed class CancelInvoiceHandler(IAppDbContext db) : IRequestHandler<CancelInvoiceCommand, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(CancelInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        if (invoice.Status == InvoiceStatuses.Cancelled)
        {
            return Error.InvalidState("This invoice is already cancelled.");
        }

        if (invoice.PaidFils > 0)
        {
            return Error.Conflict("An invoice with payments cannot be cancelled.");
        }

        invoice.Status = InvoiceStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
        return Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, invoice.Id, ct));
    }
}

public sealed class RecordPaymentHandler(IAppDbContext db, ICurrentUser current, CentreCalendar calendar, ITenantContext tenant, IPublisher publisher)
    : IRequestHandler<RecordPaymentCommand, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        if (!InvoiceStatuses.Open.Contains(invoice.Status))
        {
            return Error.InvalidState("Payments can only be recorded on an issued invoice that is not yet paid.");
        }

        if (request.AmountFils > invoice.OutstandingFils)
        {
            return Error.Validation("The payment is more than what is still owed on this invoice.", "amount_fils");
        }

        if (request.PaymentDate > calendar.Today)
        {
            return Error.Validation("The payment date cannot be in the future.", "payment_date");
        }

        db.InvoicePayments.Add(new InvoicePayment
        {
            InvoiceId = invoice.Id, AmountFils = request.AmountFils, PaymentDate = request.PaymentDate, Method = request.Method,
            Reference = request.Reference?.Trim(), Notes = request.Notes?.Trim(), RecordedBy = current.UserId,
        });
        invoice.PaidFils += request.AmountFils;
        invoice.Status = InvoiceScope.StatusAfterPayment(invoice, calendar.Today);
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new PaymentRecorded(tenant.TenantId, invoice.Id, request.AmountFils), ct);
        return Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, invoice.Id, ct));
    }
}

public sealed class SendInvoiceHandler(IAppDbContext db, IClock clock, ITenantContext tenant, IPublisher publisher) : IRequestHandler<SendInvoiceCommand, Result<InvoiceDetailDto>>
{
    public async Task<Result<InvoiceDetailDto>> Handle(SendInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        if (invoice.Status is InvoiceStatuses.Draft or InvoiceStatuses.Cancelled)
        {
            return Error.InvalidState("Issue the invoice before sending it.");
        }

        invoice.SentAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new InvoiceIssued(tenant.TenantId, invoice.Id), ct);
        return Result<InvoiceDetailDto>.Success(await InvoiceScope.DetailAsync(db, invoice.Id, ct));
    }
}

/// <summary>The invoice run, shared by the button and the scheduled job.</summary>
public sealed class InvoiceGenerator(IAppDbContext db, CentreCalendar calendar, ITenantContext tenant, BillingSettingsReader settings, IPublisher publisher)
{
    public async Task<int> GenerateAsync(DateOnly monthStart, CancellationToken ct)
    {
        var today = calendar.Today;
        var dueDays = (await settings.GetAsync(ct)).DueDays;
        var plans = await db.FeePlans.AsNoTracking().Include(p => p.Components).Where(p => p.Status == "active").ToListAsync(ct);
        if (plans.Count == 0)
        {
            return 0;
        }

        var links = await db.StudentFeePlans.AsNoTracking()
            .Where(l => l.EffectiveFrom <= monthStart.AddMonths(1).AddDays(-1) && (l.EffectiveTo == null || l.EffectiveTo >= monthStart)
                && db.Students.Any(s => s.Id == l.StudentId && s.Status == StudentStatuses.Active))
            .ToListAsync(ct);
        var existing = await db.Invoices.AsNoTracking().Where(i => i.FeePlanId != null && i.Status != InvoiceStatuses.Cancelled)
            .Select(i => new { i.StudentId, PlanId = i.FeePlanId!.Value, i.PeriodStart, i.PeriodEnd }).ToListAsync(ct);

        var sequence = await InvoiceScope.LastNumberAsync(db, today.Year, ct);
        var created = new List<Invoice>();
        foreach (var link in links)
        {
            var plan = plans.FirstOrDefault(p => p.Id == link.FeePlanId);
            if (plan is null)
            {
                continue;
            }

            var months = plan.Period switch { FeePeriods.Termly => 4, FeePeriods.Annual => 12, _ => 1 };
            var periodEnd = monthStart.AddMonths(months).AddDays(-1);
            var mine = existing.Where(e => e.StudentId == link.StudentId && e.PlanId == plan.Id).ToList();
            var due = plan.Period switch
            {
                FeePeriods.OneTime => mine.Count == 0,
                FeePeriods.Monthly => !mine.Any(e => e.PeriodStart == monthStart),
                _ => !mine.Any(e => e.PeriodEnd >= monthStart),
            };
            if (!due || created.Any(c => c.StudentId == link.StudentId && c.FeePlanId == plan.Id))
            {
                continue;
            }

            var invoice = new Invoice
            {
                StudentId = link.StudentId, FeePlanId = plan.Id, IssueDate = today, DueDate = today.AddDays(dueDays),
                PeriodStart = monthStart, PeriodEnd = periodEnd, Status = InvoiceStatuses.Issued,
            };
            var order = 0;
            long discount = 0;
            foreach (var component in plan.Components.Where(c => !c.IsDeleted).OrderBy(c => c.SortOrder))
            {
                if (component.ComponentType == ComponentTypes.Discount)
                {
                    discount += component.AmountFils;
                    continue;
                }

                invoice.Lines.Add(new InvoiceLine
                {
                    Description = component.Name, Quantity = 1, UnitPriceFils = component.AmountFils,
                    TaxFils = InvoiceMath.TaxOn(component.AmountFils, component.TaxRatePercent), FeeComponentId = component.Id, SortOrder = order++,
                });
            }

            InvoiceMath.Recalculate(invoice, Math.Min(discount, invoice.Lines.Sum(l => l.UnitPriceFils)));
            invoice.InvoiceNumber = InvoiceScope.Number(today.Year, ++sequence);
            db.Invoices.Add(invoice);
            created.Add(invoice);
        }

        if (created.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var invoice in created)
            {
                await publisher.Publish(new InvoiceIssued(tenant.TenantId, invoice.Id), ct);
            }
        }

        return created.Count;
    }

    public async Task<int> MarkOverdueAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var late = await db.Invoices.Where(i => (i.Status == InvoiceStatuses.Issued || i.Status == InvoiceStatuses.PartiallyPaid) && i.DueDate < today).ToListAsync(ct);
        late.ForEach(i => i.Status = InvoiceStatuses.Overdue);
        if (late.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return late.Count;
    }
}

public sealed class GenerateInvoicesHandler(InvoiceGenerator generator, CentreCalendar calendar) : IRequestHandler<GenerateInvoicesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(GenerateInvoicesCommand request, CancellationToken ct)
    {
        var start = request.Month is { } m
            ? DateOnly.ParseExact(m + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : new DateOnly(calendar.Today.Year, calendar.Today.Month, 1);
        return Result<int>.Success(await generator.GenerateAsync(start, ct));
    }
}

public sealed class RunBillingHandler(InvoiceGenerator generator, CentreCalendar calendar) : IRequestHandler<RunBillingCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunBillingCommand request, CancellationToken ct)
    {
        var made = await generator.GenerateAsync(new DateOnly(calendar.Today.Year, calendar.Today.Month, 1), ct);
        return Result<int>.Success(made + await generator.MarkOverdueAsync(ct));
    }
}

public sealed class InvoicePdfHandler(IAppDbContext db, IAccessScope scope, ITenantContext tenant, IInvoiceRenderer renderer)
    : IRequestHandler<InvoicePdfQuery, Result<InvoicePdfFile>>
{
    public async Task<Result<InvoicePdfFile>> Handle(InvoicePdfQuery request, CancellationToken ct)
    {
        if (request.Language is not ("en" or "ar"))
        {
            return Error.Validation("Language must be en or ar.", "language");
        }

        var invoice = await (await InvoiceScope.ScopeAsync(db.Invoices.AsNoTracking(), scope, ct)).Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        var student = await db.Students.AsNoTracking().Where(s => s.Id == invoice.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) ?? "";
        var guardians = await db.StudentGuardians.AsNoTracking().Where(g => g.StudentId == invoice.StudentId && g.Guardian!.IsPrimaryContact)
            .Select(g => g.Guardian!.FirstName + " " + g.Guardian.LastName).ToListAsync(ct);
        var centre = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Name).FirstAsync(ct);
        string Pick(Dictionary<string, string>? d) => d is null ? "" : d.TryGetValue(request.Language, out var v) ? v : d.GetValueOrDefault("en") ?? d.Values.FirstOrDefault() ?? "";

        var data = new InvoicePdfData(
            Pick(centre), invoice.InvoiceNumber, student, guardians, invoice.IssueDate, invoice.DueDate, invoice.PeriodStart, invoice.PeriodEnd, invoice.Status,
            request.Language,
            invoice.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(l => new InvoicePdfLine(Pick(l.Description), l.Quantity, l.UnitPriceFils, l.DiscountFils, l.TaxFils, l.TotalFils)).ToList(),
            invoice.SubtotalFils, invoice.DiscountFils, invoice.TaxFils, invoice.TotalFils, invoice.PaidFils, invoice.Notes is null ? null : Pick(invoice.Notes));
        return Result<InvoicePdfFile>.Success(new InvoicePdfFile(renderer.Render(data), $"{invoice.InvoiceNumber}.pdf"));
    }
}

/// <summary>Tells the child's family when an invoice is issued or sent, and when a payment is received.</summary>
public sealed class BillingNotifications(Notifier notifier, IAppDbContext db, ITenantContext tenant) :
    INotificationHandler<InvoiceIssued>,
    INotificationHandler<PaymentRecorded>
{
    public async Task Handle(InvoiceIssued n, CancellationToken ct)
    {
        tenant.TenantId = n.TenantId;
        var invoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == n.InvoiceId, ct);
        var users = await FamilyUsersAsync(invoice.StudentId, ct);
        var amount = (invoice.TotalFils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        await notifier.NotifyAsync(users, NMS.Domain.Notifications.NotificationTiers.Informational, "invoice_created",
            new() { ["en"] = $"New invoice {invoice.InvoiceNumber}", ["ar"] = $"فاتورة جديدة {invoice.InvoiceNumber}" },
            new()
            {
                ["en"] = $"Amount AED {amount}, due {invoice.DueDate:yyyy-MM-dd}. Open Billing to see the details.",
                ["ar"] = $"المبلغ {amount} درهم، تاريخ الاستحقاق {invoice.DueDate:yyyy-MM-dd}. افتح الفواتير لعرض التفاصيل.",
            }, "/billing", ct);
    }

    public async Task Handle(PaymentRecorded n, CancellationToken ct)
    {
        tenant.TenantId = n.TenantId;
        var invoice = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == n.InvoiceId, ct);
        var users = await FamilyUsersAsync(invoice.StudentId, ct);
        var amount = (n.AmountFils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        await notifier.NotifyAsync(users, NMS.Domain.Notifications.NotificationTiers.Informational, "payment_received",
            new() { ["en"] = $"Payment received for {invoice.InvoiceNumber}", ["ar"] = $"تم استلام دفعة للفاتورة {invoice.InvoiceNumber}" },
            new() { ["en"] = $"We received AED {amount}. Thank you.", ["ar"] = $"استلمنا {amount} درهم. شكراً لكم." }, "/billing", ct);
    }

    private async Task<List<Guid>> FamilyUsersAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentGuardians.AsNoTracking()
            .Where(sg => sg.StudentId == studentId && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);
}
