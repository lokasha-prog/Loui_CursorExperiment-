using NMS.Domain.Common;

namespace NMS.Domain.Attendance;

public static class PersonTypes
{
    public const string Student = "student";
    public const string Staff = "staff";

    public static readonly IReadOnlyList<string> All = [Student, Staff];
}

public static class AttendanceStatuses
{
    public const string Pending = "pending";
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Late = "late";
    public const string EarlyLeave = "early_leave";

    public static readonly IReadOnlyList<string> All = [Pending, Present, Absent, Late, EarlyLeave];
}

public static class CheckMethods
{
    public const string Barcode = "barcode";
    public const string Manual = "manual";

    public static readonly IReadOnlyList<string> All = [Barcode, Manual];
}

/// <summary>One person's attendance for one day (spec 07 §2.3). Check-in and check-out live on the same row.</summary>
public class AttendanceRecord : TenantEntity
{
    public string PersonType { get; set; } = PersonTypes.Student;

    public Guid PersonId { get; set; }

    public Guid? RoomId { get; set; }

    public DateOnly Date { get; set; }

    public DateTimeOffset? CheckInAt { get; set; }

    public Guid? CheckInBy { get; set; }

    public string? CheckInMethod { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public Guid? CheckOutBy { get; set; }

    public string? CheckOutMethod { get; set; }

    /// <summary>For students: the guardian who collected the child.</summary>
    public Guid? CheckOutGuardianId { get; set; }

    public bool? PickupIdentityVerified { get; set; }

    public string Status { get; set; } = AttendanceStatuses.Pending;

    public string? Notes { get; set; }

    public bool IsPresentNow => CheckInAt is not null && CheckOutAt is null;
}

public static class AttendanceEventTypes
{
    public const string CheckIn = "check_in";
    public const string CheckOut = "check_out";
    public const string FailedLookup = "failed_lookup";
    public const string AlreadyCheckedIn = "already_checked_in";
    public const string AlreadyCheckedOut = "already_checked_out";
}

public static class AttendanceOutcomes
{
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Error = "error";
}

/// <summary>Immutable scan log: every attempt is recorded, including failures. Never updated or deleted (trigger-enforced).</summary>
public class AttendanceEvent : Entity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string PersonType { get; set; } = PersonTypes.Student;

    public Guid? PersonId { get; set; }

    public string BarcodeScanned { get; set; } = string.Empty;

    public string EventType { get; set; } = AttendanceEventTypes.CheckIn;

    public DateTimeOffset ScannedAt { get; set; }

    public Guid ScannedBy { get; set; }

    public string? ScannerDevice { get; set; }

    public string? IpAddress { get; set; }

    public string Outcome { get; set; } = AttendanceOutcomes.Success;

    public string? Message { get; set; }
}

public static class LeaveAppliesTo
{
    public const string Student = "student";
    public const string Staff = "staff";
    public const string Both = "both";

    public static readonly IReadOnlyList<string> All = [Student, Staff, Both];
}

public class LeaveType : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public string AppliesTo { get; set; } = LeaveAppliesTo.Both;

    public bool RequiresApproval { get; set; } = true;

    /// <summary>When true an approved leave counts as an excused absence.</summary>
    public bool AffectsAttendance { get; set; } = true;

    /// <summary>For staff: an approved leave removes the person from the room ratio.</summary>
    public bool AffectsRatio { get; set; }
}

public static class LeaveStatuses
{
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = [Submitted, Approved, Rejected, Cancelled];
}

public class LeaveRequest : TenantEntity
{
    public string PersonType { get; set; } = PersonTypes.Student;

    public Guid PersonId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = LeaveStatuses.Submitted;

    public Guid? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? ApprovalNotes { get; set; }
}

/// <summary>One period during which a room had fewer staff than its ratio requires (spec 02 §3.3). Open until resolved.</summary>
public class RatioBreach : TenantEntity
{
    public Guid RoomId { get; set; }

    public DateTimeOffset DetectedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public int StudentsPresent { get; set; }

    public int StaffPresent { get; set; }

    public int StaffRequired { get; set; }
}
