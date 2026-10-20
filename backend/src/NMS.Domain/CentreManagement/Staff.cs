using NMS.Domain.Common;

namespace NMS.Domain.CentreManagement;

public static class StaffStatuses
{
    public static readonly IReadOnlyList<string> All = ["active", "on_leave", "inactive", "terminated"];
}

public class Staff : TenantEntity
{
    public Guid? AppUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Dictionary<string, string>? DisplayName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? JobTitle { get; set; }

    public DateOnly HireDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = "active";

    public string Barcode { get; set; } = string.Empty;

    [Sensitive]
    public string? EmiratesId { get; set; }

    [Sensitive]
    public string? PassportNumber { get; set; }

    /// <summary>Integer AED fils (1 AED = 100 fils), stored encrypted as text.</summary>
    [Sensitive]
    public string? SalaryAmountFils { get; set; }
}

public class StaffRoomAssignment : TenantEntity
{
    public Guid StaffId { get; set; }

    public Guid RoomId { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}
