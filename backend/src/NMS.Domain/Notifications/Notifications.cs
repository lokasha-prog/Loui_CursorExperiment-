using NMS.Domain.Common;

namespace NMS.Domain.Notifications;

/// <summary>How urgent a notification is (spec 15): critical goes everywhere, informational is quiet.</summary>
public static class NotificationTiers
{
    public const string Critical = "critical";
    public const string Operational = "operational";
    public const string Informational = "informational";

    public static readonly IReadOnlyList<string> All = [Critical, Operational, Informational];
}

public static class NotificationChannels
{
    public const string InApp = "in_app";
    public const string Email = "email";
    public const string Sms = "sms";
}

public static class DeliveryStatuses
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

public class Notification : TenantEntity
{
    public Guid UserId { get; set; }

    public string Tier { get; set; } = NotificationTiers.Operational;

    /// <summary>A short machine name such as ratio_breach, leave_submitted, message_received, daily_report.</summary>
    public string Type { get; set; } = string.Empty;

    public Dictionary<string, string> Title { get; set; } = [];

    public Dictionary<string, string> Body { get; set; } = [];

    /// <summary>Where the notification points, for example /attendance or /messages.</summary>
    public string? Link { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

public class NotificationDelivery : TenantEntity
{
    public Guid NotificationId { get; set; }

    public string Channel { get; set; } = NotificationChannels.Email;

    public string Status { get; set; } = DeliveryStatuses.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? Recipient { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}

/// <summary>A person's choice for one tier. Critical alerts cannot be switched off completely (in-app stays on).</summary>
public class NotificationPreference : TenantEntity
{
    public Guid UserId { get; set; }

    public string Tier { get; set; } = NotificationTiers.Operational;

    public bool InApp { get; set; } = true;

    public bool Email { get; set; }
}
