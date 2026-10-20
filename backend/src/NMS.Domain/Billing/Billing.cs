using NMS.Domain.Common;

namespace NMS.Domain.Billing;

public static class FeePeriods
{
    public const string Monthly = "monthly";
    public const string Termly = "termly";
    public const string Annual = "annual";
    public const string OneTime = "one_time";

    public static readonly IReadOnlyList<string> All = [Monthly, Termly, Annual, OneTime];
}

public static class ComponentTypes
{
    public const string Discount = "discount";

    public static readonly IReadOnlyList<string> All = ["tuition", "meal", "transport", "activity", Discount, "other"];
}

/// <summary>A price list for a kind of enrolment, for example "Full day, monthly". Amounts are whole fils (1 AED = 100 fils).</summary>
public class FeePlan : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }

    public string Status { get; set; } = "active";

    public string Period { get; set; } = FeePeriods.Monthly;

    public Guid? ProgramId { get; set; }

    public List<FeeComponent> Components { get; set; } = [];
}

public class FeeComponent : TenantEntity
{
    public Guid FeePlanId { get; set; }

    public Dictionary<string, string> Name { get; set; } = [];

    public string ComponentType { get; set; } = "tuition";

    public long AmountFils { get; set; }

    /// <summary>VAT or other tax as a percentage, for example 5 for 5 percent. Null means no tax.</summary>
    public decimal? TaxRatePercent { get; set; }

    public int SortOrder { get; set; }
}

public class StudentFeePlan : ITenantEntity
{
    public Guid StudentId { get; set; }

    public Guid FeePlanId { get; set; }

    public Guid TenantId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

public static class InvoiceStatuses
{
    public const string Draft = "draft";
    public const string Issued = "issued";
    public const string PartiallyPaid = "partially_paid";
    public const string Paid = "paid";
    public const string Overdue = "overdue";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = [Draft, Issued, PartiallyPaid, Paid, Overdue, Cancelled];

    /// <summary>Statuses where money is still expected.</summary>
    public static readonly IReadOnlyList<string> Open = [Issued, PartiallyPaid, Overdue];
}

public class Invoice : TenantEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid StudentId { get; set; }

    public Guid? FeePlanId { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public long SubtotalFils { get; set; }

    public long DiscountFils { get; set; }

    public long TaxFils { get; set; }

    public long TotalFils { get; set; }

    public long PaidFils { get; set; }

    public string Status { get; set; } = InvoiceStatuses.Draft;

    public DateTimeOffset? SentAt { get; set; }

    public Dictionary<string, string>? Notes { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];

    public List<InvoicePayment> Payments { get; set; } = [];

    public long OutstandingFils => TotalFils - PaidFils;
}

public class InvoiceLine : TenantEntity
{
    public Guid InvoiceId { get; set; }

    public Dictionary<string, string> Description { get; set; } = [];

    public decimal Quantity { get; set; } = 1;

    public long UnitPriceFils { get; set; }

    public long DiscountFils { get; set; }

    public long TaxFils { get; set; }

    public long TotalFils { get; set; }

    public Guid? FeeComponentId { get; set; }

    public int SortOrder { get; set; }
}

public static class PaymentMethods
{
    public static readonly IReadOnlyList<string> All = ["cash", "cheque", "bank_transfer", "card", "other"];
}

public class InvoicePayment : TenantEntity
{
    public Guid InvoiceId { get; set; }

    public long AmountFils { get; set; }

    public DateOnly PaymentDate { get; set; }

    public string Method { get; set; } = "cash";

    public string? Reference { get; set; }

    public Guid RecordedBy { get; set; }

    public string? Notes { get; set; }
}

/// <summary>The money arithmetic for an invoice. Everything is whole fils and rounds half away from zero, so totals always add up.</summary>
public static class InvoiceMath
{
    public static long Round(decimal value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

    public static long TaxOn(long taxableFils, decimal? ratePercent) => ratePercent is null ? 0 : Round(taxableFils * ratePercent.Value / 100m);

    public static void Recalculate(Invoice invoice, long discountFils)
    {
        foreach (var line in invoice.Lines)
        {
            var gross = Round(line.Quantity * line.UnitPriceFils);
            line.TotalFils = gross - line.DiscountFils + line.TaxFils;
        }

        invoice.SubtotalFils = invoice.Lines.Sum(l => Round(l.Quantity * l.UnitPriceFils));
        invoice.DiscountFils = discountFils + invoice.Lines.Sum(l => l.DiscountFils);
        invoice.TaxFils = invoice.Lines.Sum(l => l.TaxFils);
        invoice.TotalFils = invoice.SubtotalFils - invoice.DiscountFils + invoice.TaxFils;
    }
}

public static class ExpenseCategories
{
    public static readonly string[] All = ["rent", "utilities", "salaries", "supplies", "food", "other"];
}

/// <summary>Money the centre spent (rent, salaries, supplies...). Amounts are whole fils, like everything else in billing.</summary>
public class Expense : TenantEntity
{
    public DateOnly ExpenseDate { get; set; }

    public long AmountFils { get; set; }

    public string Category { get; set; } = "other";

    public string Description { get; set; } = string.Empty;

    public Guid? ReceiptMediaId { get; set; }

    public Guid RecordedBy { get; set; }
}

/// <summary>One row per centre: how billing behaves there (due days, reminder schedule and wording). Absent means the defaults apply.</summary>
public class BillingSettings : TenantEntity
{
    public int DueDays { get; set; } = 14;

    public bool RemindersEnabled { get; set; } = true;

    /// <summary>Comma-separated days before the due date to remind, for example "3,1".</summary>
    public string BeforeDueDays { get; set; } = "3,1";

    /// <summary>Comma-separated days after the due date to chase, for example "1,7".</summary>
    public string AfterDueDays { get; set; } = "1,7";

    public Dictionary<string, string>? BeforeSubject { get; set; }

    public Dictionary<string, string>? BeforeBody { get; set; }

    public Dictionary<string, string>? OverdueSubject { get; set; }

    public Dictionary<string, string>? OverdueBody { get; set; }
}

public static class ReminderKinds
{
    public const string BeforeDue = "before_due";
    public const string Overdue = "overdue";
}

/// <summary>Records every reminder so the same one is never sent twice (spec 03 §8.4 reminder log).</summary>
public class ReminderLog : TenantEntity
{
    public Guid InvoiceId { get; set; }

    public string Kind { get; set; } = ReminderKinds.BeforeDue;

    public int OffsetDays { get; set; }

    public int Recipients { get; set; }

    public string Status { get; set; } = "sent";
}
