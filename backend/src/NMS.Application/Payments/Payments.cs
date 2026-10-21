using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Billing;
using NMS.Application.Security;
using NMS.Domain.Billing;
using NMS.Shared.Results;

namespace NMS.Application.Payments;

public sealed class PaymentOptions
{
    public const string Section = "Payments";

    /// <summary>"none" keeps online payment switched off. "sandbox" is a pretend payment page for development only. "telr" uses the Telr hosted payment page.</summary>
    public string Provider { get; set; } = "none";

    public string Currency { get; set; } = "AED";

    /// <summary>Where the parent lands after paying (the web Billing page).</summary>
    public string ReturnBaseUrl { get; set; } = "http://localhost:5173/billing";

    /// <summary>The public address of this API, used to build the payment page and return links.</summary>
    public string PublicApiUrl { get; set; } = "http://localhost:5178";

    /// <summary>Shared secret used to sign and check the sandbox confirmations. Keep it in user secrets or the server settings.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public string TelrStoreId { get; set; } = string.Empty;

    public string TelrAuthKey { get; set; } = string.Empty;

    public string TelrRemoteKey { get; set; } = string.Empty;

    public bool TelrTestMode { get; set; } = true;

    public int PendingMinutesBeforeCheck { get; set; } = 20;

    public int PendingHoursBeforeExpiry { get; set; } = 24;
}

public sealed record CardInfo(string Token, string Brand, string Last4, int ExpiryMonth, int ExpiryYear);

public sealed record GatewaySessionRequest(
    string Reference, long AmountFils, string Currency, string Description, string ReturnUrl, string PayerEmail, string PayerName, bool SaveCard, string? SavedToken);

/// <summary>What the payment company gave back for a new payment: where to send the parent, or (for a saved card) the final answer straight away.</summary>
public sealed record GatewaySessionResult(string GatewayReference, string? RedirectUrl, string? ImmediateStatus, CardInfo? Card);

/// <summary>A confirmation from the payment company. When it could not be proved genuine on its own, <see cref="NeedsVerification"/> is set and the system asks the company directly.</summary>
public sealed record GatewayEventData(string EventId, string Reference, string Status, long AmountFils, string? GatewayReference, CardInfo? Card, bool NeedsVerification);

public sealed record GatewayStatus(string Status, long AmountFils, CardInfo? Card, string? Reason);

public static class GatewayOutcomes
{
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Pending = "pending";
}

/// <summary>One payment company. Implemented outside this layer so no company-specific code leaks in here.</summary>
public interface IPaymentGateway
{
    string Name { get; }

    bool SupportsSavedCards { get; }

    bool IsConfigured { get; }

    Task<Result<GatewaySessionResult>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken ct);

    /// <summary>Asks the company directly what happened to a payment. This is the check that cannot be faked by a visitor.</summary>
    Task<GatewayStatus> GetStatusAsync(string? gatewayReference, string reference, CancellationToken ct);

    Task<Result<string>> RefundAsync(string? gatewayReference, string reference, long amountFils, string currency, string reason, CancellationToken ct);

    /// <summary>Checks the signature (if the company signs) and reads the message. A failure means the message is not genuine.</summary>
    Result<GatewayEventData> ParseWebhook(IReadOnlyDictionary<string, string> headers, string body);
}

public sealed class PaymentGateways(IEnumerable<IPaymentGateway> all, IOptions<PaymentOptions> options)
{
    public IPaymentGateway? Current => all.FirstOrDefault(g => g.Name == options.Value.Provider && g.IsConfigured);

    public IPaymentGateway? ByName(string name) => all.FirstOrDefault(g => g.Name == name && g.IsConfigured);

    public static string NewReference(Guid tenantId) => $"{tenantId:N}-{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";

    public static bool TryTenantOf(string? reference, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return reference is { Length: 49 } && reference[32] == '-' && Guid.TryParseExact(reference[..32], "N", out tenantId);
    }
}

public sealed record PaymentSessionDto(
    Guid Id, Guid InvoiceId, string? InvoiceNumber, string Reference, string Gateway, long AmountFils, long RefundedFils, string Currency,
    string Status, string? FailureReason, string? PayerName, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record PaymentConfigDto(bool Enabled, string Provider, string Currency, bool SavedCards);

public sealed record StartedPaymentDto(Guid SessionId, string Reference, string? RedirectUrl, string Status);

public sealed record SavedMethodDto(Guid Id, string Brand, string Last4, int ExpiryMonth, int ExpiryYear);

internal static class PaymentMaps
{
    public static async Task<List<PaymentSessionDto>> ToDtosAsync(IAppDbContext db, List<PaymentSession> sessions, CancellationToken ct)
    {
        var invoiceIds = sessions.Select(s => s.InvoiceId).Distinct().ToList();
        var numbers = await db.Invoices.AsNoTracking().Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber, ct);
        var payerIds = sessions.Select(s => s.PayerUserId).Distinct().ToList();
        var payers = await db.AppUsers.AsNoTracking().Where(u => payerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return sessions.Select(s => new PaymentSessionDto(
            s.Id, s.InvoiceId, numbers.GetValueOrDefault(s.InvoiceId), s.Reference, s.Gateway, s.AmountFils, s.RefundedFils, s.Currency, s.Status,
            s.FailureReason, payers.GetValueOrDefault(s.PayerUserId), s.CreatedAt, s.CompletedAt)).ToList();
    }
}

/// <summary>
/// The one place where a confirmed outcome turns into money on an invoice. The confirmation message, the return page and the daily check all end up here,
/// so an outcome is applied once however many times it arrives.
/// </summary>
public sealed class PaymentApplier(IAppDbContext db, CentreCalendar calendar, IClock clock, ITenantContext tenant, IPublisher publisher)
{
    public async Task ApplyAsync(PaymentSession session, string outcome, long amountFils, string? gatewayReference, CardInfo? card, string? reason, CancellationToken ct)
    {
        if (session.Status != PaymentStatuses.Pending)
        {
            return;
        }

        session.GatewayReference ??= gatewayReference;
        if (outcome is GatewayOutcomes.Failed or GatewayOutcomes.Cancelled)
        {
            session.Status = outcome == GatewayOutcomes.Failed ? PaymentStatuses.Failed : PaymentStatuses.Cancelled;
            session.FailureReason = reason;
            session.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (outcome != GatewayOutcomes.Paid)
        {
            return;
        }

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == session.InvoiceId, ct);
        string? problem = null;
        if (amountFils != session.AmountFils)
        {
            problem = "The amount the payment company confirmed is different from the amount asked for.";
        }
        else if (invoice is null || !InvoiceStatuses.Open.Contains(invoice.Status) || invoice.OutstandingFils < session.AmountFils)
        {
            problem = "The invoice was already settled or closed when the payment arrived.";
        }

        if (problem is not null)
        {
            session.Status = PaymentStatuses.NeedsReview;
            session.FailureReason = problem;
            session.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var payment = new InvoicePayment
        {
            InvoiceId = invoice!.Id, AmountFils = session.AmountFils, PaymentDate = calendar.Today, Method = "card",
            Reference = (session.GatewayReference ?? session.Reference)[..Math.Min(100, (session.GatewayReference ?? session.Reference).Length)],
            Notes = "Online payment", RecordedBy = session.PayerUserId,
        };
        db.InvoicePayments.Add(payment);
        invoice.PaidFils += session.AmountFils;
        invoice.Status = InvoiceScope.StatusAfterPayment(invoice, calendar.Today);
        session.Status = PaymentStatuses.Paid;
        session.PaymentId = payment.Id;
        session.CompletedAt = clock.UtcNow;

        if (session.SaveCard && card is not null && !await db.SavedPaymentMethods.AnyAsync(m => m.UserId == session.PayerUserId && m.Token == card.Token, ct))
        {
            db.SavedPaymentMethods.Add(new SavedPaymentMethod
            {
                UserId = session.PayerUserId, Gateway = session.Gateway, Token = card.Token, Brand = card.Brand, Last4 = card.Last4,
                ExpiryMonth = card.ExpiryMonth, ExpiryYear = card.ExpiryYear,
            });
        }

        await db.SaveChangesAsync(ct);
        await publisher.Publish(new PaymentRecorded(tenant.TenantId, invoice.Id, session.AmountFils), ct);
    }
}

// ---------------------------------------------------------------- is online payment on

public sealed record PaymentConfigQuery : IRequest<Result<PaymentConfigDto>>;

public sealed class PaymentConfigHandler(PaymentGateways gateways, IOptions<PaymentOptions> options) : IRequestHandler<PaymentConfigQuery, Result<PaymentConfigDto>>
{
    public Task<Result<PaymentConfigDto>> Handle(PaymentConfigQuery request, CancellationToken ct)
    {
        var gateway = gateways.Current;
        return Task.FromResult(Result<PaymentConfigDto>.Success(new PaymentConfigDto(gateway is not null, options.Value.Provider, options.Value.Currency, gateway?.SupportsSavedCards ?? false)));
    }
}

// ---------------------------------------------------------------- start a payment

public sealed record StartPaymentCommand(Guid InvoiceId, long? AmountFils, bool SaveCard, Guid? SavedMethodId) : IRequest<Result<StartedPaymentDto>>;

public sealed class StartPaymentValidator : AbstractValidator<StartPaymentCommand>
{
    public StartPaymentValidator() => RuleFor(c => c.AmountFils!.Value).GreaterThan(0).When(c => c.AmountFils is not null);
}

public sealed class StartPaymentHandler(
    IAppDbContext db, IAccessScope scope, NMS.Application.Settings.CentreSettingsReader settings, ICurrentUser current, ITenantContext tenant,
    PaymentGateways gateways, IOptions<PaymentOptions> options, PaymentApplier applier) : IRequestHandler<StartPaymentCommand, Result<StartedPaymentDto>>
{
    public async Task<Result<StartedPaymentDto>> Handle(StartPaymentCommand request, CancellationToken ct)
    {
        var gateway = gateways.Current;
        if (gateway is null)
        {
            return Error.InvalidState("Online payment is not switched on for this centre yet.");
        }

        var invoices = await InvoiceScope.ScopeAsync(db.Invoices.AsNoTracking(), scope, settings, ct);
        var invoice = await invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null)
        {
            return Error.NotFound("Invoice not found.");
        }

        if (!InvoiceStatuses.Open.Contains(invoice.Status))
        {
            return Error.InvalidState("This invoice has nothing left to pay online.");
        }

        var amount = request.AmountFils ?? invoice.OutstandingFils;
        if (amount > invoice.OutstandingFils)
        {
            return Error.Validation("The amount is more than what is still owed on this invoice.", "amount_fils");
        }

        string? savedToken = null;
        if (request.SavedMethodId is { } methodId)
        {
            var method = await db.SavedPaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId && m.UserId == current.UserId && m.Gateway == gateway.Name, ct);
            if (method is null)
            {
                return Error.NotFound("Saved card not found.");
            }

            savedToken = method.Token;
        }

        var payer = await db.AppUsers.AsNoTracking().FirstAsync(u => u.Id == current.UserId, ct);
        var session = new PaymentSession
        {
            InvoiceId = invoice.Id, PayerUserId = payer.Id, Gateway = gateway.Name, Reference = PaymentGateways.NewReference(tenant.TenantId),
            AmountFils = amount, Currency = options.Value.Currency, SaveCard = request.SaveCard && gateway.SupportsSavedCards,
        };
        db.PaymentSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var returnUrl = $"{options.Value.PublicApiUrl.TrimEnd('/')}/pay/return/{session.Reference}";
        var created = await gateway.CreateSessionAsync(new GatewaySessionRequest(
            session.Reference, amount, session.Currency, $"Invoice {invoice.InvoiceNumber}", returnUrl, payer.Email, $"{payer.FirstName} {payer.LastName}".Trim(), session.SaveCard, savedToken), ct);
        if (created.IsFailure)
        {
            session.Status = PaymentStatuses.Failed;
            session.FailureReason = created.Errors[0].Message;
            await db.SaveChangesAsync(ct);
            return Result<StartedPaymentDto>.Failure(created.Errors);
        }

        session.GatewayReference = created.Value.GatewayReference;
        await db.SaveChangesAsync(ct);
        if (created.Value.ImmediateStatus is { } immediate)
        {
            await applier.ApplyAsync(session, immediate, amount, created.Value.GatewayReference, created.Value.Card, immediate == GatewayOutcomes.Paid ? null : "The saved card was not accepted.", ct);
        }

        return Result<StartedPaymentDto>.Success(new StartedPaymentDto(session.Id, session.Reference, created.Value.RedirectUrl, session.Status));
    }
}

// ---------------------------------------------------------------- a confirmation from the payment company

/// <summary>The company's server tells us what happened. Not signed in: the message itself must prove it is genuine.</summary>
public sealed record ProcessGatewayEventCommand(string Gateway, IReadOnlyDictionary<string, string> Headers, string Body) : IRequest<Result>;

public sealed class ProcessGatewayEventHandler(
    IAppDbContext db, ITenantContext tenant, PaymentGateways gateways, PaymentApplier applier) : IRequestHandler<ProcessGatewayEventCommand, Result>
{
    public async Task<Result> Handle(ProcessGatewayEventCommand request, CancellationToken ct)
    {
        var gateway = gateways.ByName(request.Gateway);
        if (gateway is null)
        {
            return Result.Failure(Error.NotFound());
        }

        var parsed = gateway.ParseWebhook(request.Headers, request.Body);
        if (parsed.IsFailure)
        {
            return Result.Failure(Error.Unauthorized("The message could not be verified."));
        }

        var data = parsed.Value;
        if (!PaymentGateways.TryTenantOf(data.Reference, out var centre))
        {
            return Result.Failure(Error.NotFound());
        }

        tenant.TenantId = centre;
        var session = await db.PaymentSessions.FirstOrDefaultAsync(s => s.Reference == data.Reference && s.Gateway == gateway.Name, ct);
        if (session is null)
        {
            return Result.Failure(Error.NotFound());
        }

        if (await db.GatewayEvents.AnyAsync(e => e.Gateway == gateway.Name && e.EventId == data.EventId, ct))
        {
            return Result.Success();
        }

        var status = data.Status;
        var amount = data.AmountFils;
        var card = data.Card;
        string? reason = null;
        if (data.NeedsVerification)
        {
            var checkedStatus = await gateway.GetStatusAsync(data.GatewayReference ?? session.GatewayReference, session.Reference, ct);
            status = checkedStatus.Status;
            amount = checkedStatus.AmountFils;
            card = checkedStatus.Card;
            reason = checkedStatus.Reason;
        }

        db.GatewayEvents.Add(new GatewayEvent { Gateway = gateway.Name, EventId = data.EventId, Reference = session.Reference, Outcome = status });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same message arrived at the same moment on another request; that one applies it.
            return Result.Success();
        }

        await applier.ApplyAsync(session, status, amount, data.GatewayReference, card, reason ?? (status == GatewayOutcomes.Paid ? null : "The payment was not completed."), ct);
        return Result.Success();
    }
}

/// <summary>The parent comes back from the payment page. The system asks the company what really happened instead of trusting the address.</summary>
public sealed record PaymentReturnCommand(string Reference) : IRequest<Result<string>>;

public sealed class PaymentReturnHandler(IAppDbContext db, ITenantContext tenant, PaymentGateways gateways, PaymentApplier applier, IOptions<PaymentOptions> options)
    : IRequestHandler<PaymentReturnCommand, Result<string>>
{
    public async Task<Result<string>> Handle(PaymentReturnCommand request, CancellationToken ct)
    {
        var back = $"{options.Value.ReturnBaseUrl}{(options.Value.ReturnBaseUrl.Contains('?') ? '&' : '?')}payment={request.Reference}";
        if (!PaymentGateways.TryTenantOf(request.Reference, out var centre))
        {
            return Result<string>.Success(options.Value.ReturnBaseUrl);
        }

        tenant.TenantId = centre;
        var session = await db.PaymentSessions.FirstOrDefaultAsync(s => s.Reference == request.Reference, ct);
        if (session is not null && session.Status == PaymentStatuses.Pending && gateways.ByName(session.Gateway) is { } gateway)
        {
            var status = await gateway.GetStatusAsync(session.GatewayReference, session.Reference, ct);
            if (status.Status != GatewayOutcomes.Pending)
            {
                await applier.ApplyAsync(session, status.Status, status.AmountFils, session.GatewayReference, status.Card, status.Reason, ct);
            }
        }

        return Result<string>.Success(back);
    }
}

// ---------------------------------------------------------------- see payments

public sealed record ListPaymentsQuery(string? Status, Guid? InvoiceId) : IRequest<Result<List<PaymentSessionDto>>>;

public sealed class ListPaymentsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListPaymentsQuery, Result<List<PaymentSessionDto>>>
{
    public async Task<Result<List<PaymentSessionDto>>> Handle(ListPaymentsQuery request, CancellationToken ct)
    {
        var query = db.PaymentSessions.AsNoTracking().AsQueryable();
        if (!current.HasPermission(Permissions.PaymentsRecord))
        {
            query = query.Where(s => s.PayerUserId == current.UserId);
        }

        if (request.Status is not null)
        {
            query = query.Where(s => s.Status == request.Status);
        }

        if (request.InvoiceId is { } invoice)
        {
            query = query.Where(s => s.InvoiceId == invoice);
        }

        var rows = await query.OrderByDescending(s => s.CreatedAt).Take(200).ToListAsync(ct);
        return Result<List<PaymentSessionDto>>.Success(await PaymentMaps.ToDtosAsync(db, rows, ct));
    }
}

public sealed record PaymentByReferenceQuery(string Reference) : IRequest<Result<PaymentSessionDto>>;

public sealed class PaymentByReferenceHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<PaymentByReferenceQuery, Result<PaymentSessionDto>>
{
    public async Task<Result<PaymentSessionDto>> Handle(PaymentByReferenceQuery request, CancellationToken ct)
    {
        var session = await db.PaymentSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Reference == request.Reference, ct);
        if (session is null || (session.PayerUserId != current.UserId && !current.HasPermission(Permissions.PaymentsRecord)))
        {
            return Error.NotFound();
        }

        return Result<PaymentSessionDto>.Success((await PaymentMaps.ToDtosAsync(db, [session], ct))[0]);
    }
}

// ---------------------------------------------------------------- refunds

public sealed record RefundPaymentCommand(Guid SessionId, long? AmountFils, string Reason) : IRequest<Result<PaymentSessionDto>>;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(300);
        RuleFor(c => c.AmountFils!.Value).GreaterThan(0).When(c => c.AmountFils is not null);
    }
}

public sealed class RefundPaymentHandler(
    IAppDbContext db, ICurrentUser current, PaymentGateways gateways, CentreCalendar calendar)
    : IRequestHandler<RefundPaymentCommand, Result<PaymentSessionDto>>
{
    public async Task<Result<PaymentSessionDto>> Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        var session = await db.PaymentSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session is null)
        {
            return Error.NotFound();
        }

        if (session.RefundableFils <= 0)
        {
            return Error.InvalidState("Only a completed payment with money left can be refunded.");
        }

        var amount = request.AmountFils ?? session.RefundableFils;
        if (amount > session.RefundableFils)
        {
            return Error.Validation("The refund is more than what is left of this payment.", "amount_fils");
        }

        var gateway = gateways.ByName(session.Gateway);
        if (gateway is null)
        {
            return Error.InvalidState("The payment company for this payment is not available.");
        }

        var invoice = await db.Invoices.FirstAsync(i => i.Id == session.InvoiceId, ct);
        var refunded = await gateway.RefundAsync(session.GatewayReference, session.Reference, amount, session.Currency, request.Reason.Trim(), ct);
        if (refunded.IsFailure)
        {
            return Result<PaymentSessionDto>.Failure(refunded.Errors);
        }

        var entry = new InvoicePayment
        {
            InvoiceId = invoice.Id, AmountFils = -amount, PaymentDate = calendar.Today, Method = "card", Reference = ("refund:" + refunded.Value)[..Math.Min(100, 7 + refunded.Value.Length)],
            Notes = request.Reason.Trim(), RecordedBy = current.UserId,
        };
        db.InvoicePayments.Add(entry);
        db.PaymentRefunds.Add(new PaymentRefund { SessionId = session.Id, AmountFils = amount, Reason = request.Reason.Trim(), GatewayReference = refunded.Value, PaymentId = entry.Id });
        invoice.PaidFils -= amount;
        invoice.Status = InvoiceScope.StatusAfterPayment(invoice, calendar.Today);
        session.RefundedFils += amount;
        session.Status = session.RefundedFils >= session.AmountFils ? PaymentStatuses.Refunded : PaymentStatuses.PartiallyRefunded;
        await db.SaveChangesAsync(ct);
        return Result<PaymentSessionDto>.Success((await PaymentMaps.ToDtosAsync(db, [session], ct))[0]);
    }
}

// ---------------------------------------------------------------- saved cards

public sealed record MySavedMethodsQuery : IRequest<Result<List<SavedMethodDto>>>;

public sealed class MySavedMethodsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<MySavedMethodsQuery, Result<List<SavedMethodDto>>>
{
    public async Task<Result<List<SavedMethodDto>>> Handle(MySavedMethodsQuery request, CancellationToken ct) =>
        Result<List<SavedMethodDto>>.Success(await db.SavedPaymentMethods.AsNoTracking().Where(m => m.UserId == current.UserId)
            .OrderBy(m => m.CreatedAt).Select(m => new SavedMethodDto(m.Id, m.Brand, m.Last4, m.ExpiryMonth, m.ExpiryYear)).ToListAsync(ct));
}

public sealed record DeleteSavedMethodCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteSavedMethodHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<DeleteSavedMethodCommand, Result>
{
    public async Task<Result> Handle(DeleteSavedMethodCommand request, CancellationToken ct)
    {
        var method = await db.SavedPaymentMethods.FirstOrDefaultAsync(m => m.Id == request.Id && m.UserId == current.UserId, ct);
        if (method is null)
        {
            return Result.Failure(Error.NotFound());
        }

        method.IsDeleted = true;
        method.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------- daily check against the payment company

public sealed record ReconciliationIssueDto(Guid SessionId, string Reference, string Problem, long AmountFils, string Status, DateTimeOffset CreatedAt);

public sealed record ReconciliationDto(
    int PaidCount, long PaidFils, long RefundedFils, int PendingCount, int NeedsReviewCount, IReadOnlyList<ReconciliationIssueDto> Issues);

public sealed record PaymentReconciliationQuery : IRequest<Result<ReconciliationDto>>;

/// <summary>What the payment company says was paid, against what is recorded on invoices. Anything that does not agree is listed for a person.</summary>
public sealed class PaymentReconciliationHandler(IAppDbContext db, IClock clock, IOptions<PaymentOptions> options) : IRequestHandler<PaymentReconciliationQuery, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(PaymentReconciliationQuery request, CancellationToken ct)
    {
        var since = clock.UtcNow.AddDays(-60);
        var sessions = await db.PaymentSessions.AsNoTracking().Where(s => s.CreatedAt >= since).ToListAsync(ct);
        var paymentIds = sessions.Where(s => s.PaymentId != null).Select(s => s.PaymentId!.Value).ToList();
        var recorded = await db.InvoicePayments.AsNoTracking().Where(p => paymentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.AmountFils, ct);
        var stale = clock.UtcNow.AddMinutes(-options.Value.PendingMinutesBeforeCheck);
        var issues = new List<ReconciliationIssueDto>();
        foreach (var s in sessions)
        {
            string? problem = null;
            if (s.Status == PaymentStatuses.NeedsReview)
            {
                problem = s.FailureReason ?? "Needs a person to look at it.";
            }
            else if (s.Status is PaymentStatuses.Paid or PaymentStatuses.PartiallyRefunded or PaymentStatuses.Refunded
                && (s.PaymentId is null || recorded.GetValueOrDefault(s.PaymentId.Value) != s.AmountFils))
            {
                problem = "The amount recorded on the invoice does not match the payment.";
            }
            else if (s.Status == PaymentStatuses.Pending && s.CreatedAt < stale)
            {
                problem = "Still waiting for the payment company to confirm.";
            }

            if (problem is not null)
            {
                issues.Add(new ReconciliationIssueDto(s.Id, s.Reference, problem, s.AmountFils, s.Status, s.CreatedAt));
            }
        }

        var paid = sessions.Where(s => s.Status is PaymentStatuses.Paid or PaymentStatuses.PartiallyRefunded or PaymentStatuses.Refunded).ToList();
        return Result<ReconciliationDto>.Success(new ReconciliationDto(
            paid.Count, paid.Sum(s => s.AmountFils), sessions.Sum(s => s.RefundedFils), sessions.Count(s => s.Status == PaymentStatuses.Pending),
            sessions.Count(s => s.Status == PaymentStatuses.NeedsReview), issues));
    }
}

/// <summary>Scheduled job, per centre: asks the payment company about payments still waiting, and closes ones that never completed.</summary>
public sealed record RunPaymentReconciliationCommand : IRequest<Result<int>>;

public sealed class RunPaymentReconciliationHandler(IAppDbContext db, IClock clock, PaymentGateways gateways, PaymentApplier applier, IOptions<PaymentOptions> options)
    : IRequestHandler<RunPaymentReconciliationCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunPaymentReconciliationCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var checkBefore = now.AddMinutes(-options.Value.PendingMinutesBeforeCheck);
        var pending = await db.PaymentSessions.Where(s => s.Status == PaymentStatuses.Pending && s.CreatedAt < checkBefore).Take(200).ToListAsync(ct);
        var changed = 0;
        foreach (var session in pending)
        {
            var gateway = gateways.ByName(session.Gateway);
            var status = gateway is null ? new GatewayStatus(GatewayOutcomes.Pending, 0, null, null) : await gateway.GetStatusAsync(session.GatewayReference, session.Reference, ct);
            if (status.Status != GatewayOutcomes.Pending)
            {
                await applier.ApplyAsync(session, status.Status, status.AmountFils, session.GatewayReference, status.Card, status.Reason, ct);
                changed++;
            }
            else if (session.CreatedAt < now.AddHours(-options.Value.PendingHoursBeforeExpiry))
            {
                session.Status = PaymentStatuses.Expired;
                session.FailureReason = "The payment was never completed.";
                session.CompletedAt = now;
                await db.SaveChangesAsync(ct);
                changed++;
            }
        }

        return Result<int>.Success(changed);
    }
}

// ---------------------------------------------------------------- the pretend payment page (development only)

public sealed record SandboxPageDto(string Reference, long AmountFils, string Currency, string? InvoiceNumber, string Status, bool CanSaveCard);

public sealed record SandboxPageQuery(string Reference) : IRequest<Result<SandboxPageDto>>;

public sealed class SandboxPageHandler(IAppDbContext db, ITenantContext tenant, PaymentGateways gateways) : IRequestHandler<SandboxPageQuery, Result<SandboxPageDto>>
{
    public async Task<Result<SandboxPageDto>> Handle(SandboxPageQuery request, CancellationToken ct)
    {
        var gateway = gateways.ByName("sandbox");
        if (gateway is null || !PaymentGateways.TryTenantOf(request.Reference, out var centre))
        {
            return Error.NotFound();
        }

        tenant.TenantId = centre;
        var session = await db.PaymentSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Reference == request.Reference && s.Gateway == "sandbox", ct);
        if (session is null)
        {
            return Error.NotFound();
        }

        var number = await db.Invoices.AsNoTracking().Where(i => i.Id == session.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefaultAsync(ct);
        return Result<SandboxPageDto>.Success(new SandboxPageDto(session.Reference, session.AmountFils, session.Currency, number, session.Status, session.SaveCard));
    }
}
