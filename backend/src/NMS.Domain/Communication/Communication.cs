using NMS.Domain.Common;

namespace NMS.Domain.Communication;

public static class MediaStatuses
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>An uploaded photo or video. The bytes live in the SQL Server database (golden rule: no other store).</summary>
public class MediaFile : TenantEntity
{
    public Guid UploadedBy { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ScanStatus { get; set; } = MediaStatuses.Approved;

    public string? ScanNote { get; set; }

    public byte[] Content { get; set; } = [];
}

public static class ActivityTypes
{
    public const string Announcement = "announcement";
    public const string Photo = "photo";
    public const string Video = "video";
    public const string Diary = "diary";
    public const string Food = "food";
    public const string Toilet = "toilet";
    public const string Medical = "medical";
    public const string Mood = "mood";

    public static readonly IReadOnlyList<string> All = [Announcement, Photo, Video, Diary, Food, Toilet, Medical, Mood];

    /// <summary>Announcements and medical notes always wait for a manager; everything else a teacher posts is shown straight away.</summary>
    public static bool NeedsApproval(string type) => type is Announcement or Medical;
}

public static class ApprovalStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = [Pending, Approved, Rejected];
}

public class ActivityPost : TenantEntity
{
    public string Type { get; set; } = ActivityTypes.Diary;

    public Dictionary<string, string>? Caption { get; set; }

    public Guid PostedByUserId { get; set; }

    public Guid? RoomId { get; set; }

    public string ApprovalStatus { get; set; } = ApprovalStatuses.Pending;

    public Guid? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }
}

public class ActivityPostStudent : ITenantEntity
{
    public Guid PostId { get; set; }

    public Guid StudentId { get; set; }

    public Guid TenantId { get; set; }
}

public class ActivityPostMedia : ITenantEntity
{
    public Guid PostId { get; set; }

    public Guid MediaId { get; set; }

    public int SortOrder { get; set; }

    public Guid TenantId { get; set; }
}

/// <summary>A child's summary of one day for their family, built from attendance and approved activities.</summary>
public class DailyReport : TenantEntity
{
    public Guid StudentId { get; set; }

    public DateOnly Date { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public int ActivityCount { get; set; }

    public DateTimeOffset? CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public Dictionary<string, string> Summary { get; set; } = [];
}

public class MessageThread : TenantEntity
{
    public string Subject { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public Guid? StudentId { get; set; }

    /// <summary>A message to many families at once; families can read it but not reply.</summary>
    public bool IsBroadcast { get; set; }

    /// <summary>Started by a family: every staff member who handles messages sees it in the shared inbox.</summary>
    public bool OpenToStaff { get; set; }

    public DateTimeOffset LastMessageAt { get; set; }
}

public class ThreadParticipant : ITenantEntity
{
    public Guid ThreadId { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset? LastReadAt { get; set; }
}

public class Message : TenantEntity
{
    public Guid ThreadId { get; set; }

    public Guid SenderUserId { get; set; }

    public string Body { get; set; } = string.Empty;
}
