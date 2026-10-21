using NMS.Domain.Common;

namespace NMS.Domain.Billing;

public static class PaymentStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";

    /// <summary>Money arrived but could not be matched to the invoice (for example it was settled another way meanwhile). A person decides what to do.</summary>
    public const string NeedsReview = "needs_review";

    public const string PartiallyRefunded = "partially_refunded";
    public const string Refunded = "refunded";

    public static readonly IReadOnlyList<string> All = [Pending, Paid, Failed, Cancelled, Expired, NeedsReview, PartiallyRefunded, Refunded];
}

/// <summary>One attempt by a parent to pay an invoice online. The reference starts with the centre id so a confirmation from the payment company can be matched without signing in.</summary>
public class PaymentSession : TenantEntity
{
    public Guid InvoiceId { get; set; }

    /// <summary>The person who started the payment.</summary>
    public Guid PayerUserId { get; set; }

    public string Gateway { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string? GatewayReference { get; set; }

    public long AmountFils { get; set; }

    public string Currency { get; set; } = "AED";

    public string Status { get; set; } = PaymentStatuses.Pending;

    public string? FailureReason { get; set; }

    public bool SaveCard { get; set; }

    /// <summary>The invoice payment this became, once the money is confirmed.</summary>
    public Guid? PaymentId { get; set; }

    public long RefundedFils { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long RefundableFils => Status is PaymentStatuses.Paid or PaymentStatuses.PartiallyRefunded ? AmountFils - RefundedFils : 0;
}

public class PaymentRefund : TenantEntity
{
    public Guid SessionId { get; set; }

    public long AmountFils { get; set; }

    public string? Reason { get; set; }

    public string? GatewayReference { get; set; }

    public Guid PaymentId { get; set; }
}

/// <summary>Every confirmation from the payment company is written here first, so the same message arriving twice is applied once.</summary>
public class GatewayEvent : TenantEntity
{
    public string Gateway { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;
}

/// <summary>A card a parent chose to keep. Only the payment company's token and the last four digits are kept; the card number never reaches this system.</summary>
public class SavedPaymentMethod : TenantEntity
{
    public Guid UserId { get; set; }

    public string Gateway { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Last4 { get; set; } = string.Empty;

    public int ExpiryMonth { get; set; }

    public int ExpiryYear { get; set; }
}
