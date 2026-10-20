using NMS.Domain.Common;

namespace NMS.Domain.Admissions;

public static class EnquiryStages
{
    public const string NoStage = "no_stage";
    public const string Interested = "interested";
    public const string Contacted = "contacted";
    public const string TourAppointment = "tour_appointment";
    public const string Toured = "toured";
    public const string Enrolled = "enrolled";

    /// <summary>In pipeline order (spec 03 §10.1).</summary>
    public static readonly string[] All = [NoStage, Interested, Contacted, TourAppointment, Toured, Enrolled];
}

public static class EnquirySources
{
    public static readonly string[] All = ["walk_in", "website", "referral", "social_media", "phone", "email", "existing_parent"];
}

public static class ContactMethods
{
    public static readonly string[] All = ["phone", "whatsapp", "email"];
}

public static class ContactKinds
{
    public static readonly string[] All = ["call", "whatsapp", "email", "visit", "note"];
}

/// <summary>A family asking about a place. It moves through the pipeline until it is enrolled as a student.</summary>
public class Enquiry : TenantEntity
{
    public string FamilyName { get; set; } = string.Empty;

    public string ChildName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string PreferredContact { get; set; } = "phone";

    public DateOnly? ChildDateOfBirth { get; set; }

    public DateOnly? DesiredStartDate { get; set; }

    public Guid? ProgramId { get; set; }

    public string Source { get; set; } = "walk_in";

    public Guid? AssignedStaffId { get; set; }

    public string Stage { get; set; } = EnquiryStages.NoStage;

    public DateOnly EnquiryDate { get; set; }

    public DateOnly? LastContactDate { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Set when the enquiry was converted, so reports can tie a student back to where they came from.</summary>
    public Guid? StudentId { get; set; }
}

/// <summary>Append-only record of every stage change (spec 03 §10.2).</summary>
public class EnquiryStageLog : TenantEntity
{
    public Guid EnquiryId { get; set; }

    public string? FromStage { get; set; }

    public string ToStage { get; set; } = EnquiryStages.NoStage;

    public string? Reason { get; set; }
}

/// <summary>One contact attempt or conversation note on an enquiry.</summary>
public class EnquiryActivity : TenantEntity
{
    public Guid EnquiryId { get; set; }

    public string Kind { get; set; } = "note";

    public string Summary { get; set; } = string.Empty;
}

public static class TourStatuses
{
    public const string Scheduled = "scheduled";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string NoShow = "no_show";

    public static readonly string[] All = [Scheduled, Completed, Cancelled, NoShow];
}

public static class TourTypes
{
    public static readonly string[] All = ["general", "program_focus", "birthday_party"];
}

/// <summary>A visit booked for an enquiring family (spec 03 §9).</summary>
public class Tour : TenantEntity
{
    public Guid EnquiryId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public int DurationMinutes { get; set; } = 30;

    public string TourType { get; set; } = "general";

    public int Adults { get; set; } = 1;

    public int Children { get; set; } = 1;

    public string? Notes { get; set; }

    public string Status { get; set; } = TourStatuses.Scheduled;

    public DateTimeOffset? ReminderSentAt { get; set; }
}

/// <summary>Time a staff member cannot take tours (leave, lunch, other commitments). No staff means the whole centre is closed to tours.</summary>
public class TourBlockOff : TenantEntity
{
    public Guid? StaffId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string? Reason { get; set; }
}

/// <summary>Something a member of staff must do, such as following up with a family after a tour.</summary>
public class FollowUpTask : TenantEntity
{
    public string Title { get; set; } = string.Empty;

    public DateOnly DueDate { get; set; }

    public Guid? AssignedStaffId { get; set; }

    public Guid? EnquiryId { get; set; }

    public Guid? TourId { get; set; }

    public bool IsDone { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>An e-mail to someone who is not a system user yet (an enquiring family). Sent by the background run, retried, never lost.</summary>
public class OutboundEmail : TenantEntity
{
    public string ToAddress { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? Error { get; set; }
}
