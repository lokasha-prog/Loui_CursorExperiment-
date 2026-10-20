using NMS.Domain.Common;

namespace NMS.Domain.CentreManagement;

public class Room : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public int Capacity { get; set; }

    public int MinAgeMonths { get; set; }

    public int MaxAgeMonths { get; set; }

    /// <summary>Students allowed per staff member (1:4 is stored as 4).</summary>
    public int RatioStaffToStudent { get; set; }

    public bool IsActive { get; set; } = true;
}

public class Program : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }

    public int DaysPerWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string AgeGroup { get; set; } = string.Empty;

    /// <summary>Children whose age in months is outside this range cannot be placed in the programme.</summary>
    public int MinAgeMonths { get; set; }

    public int MaxAgeMonths { get; set; } = 120;

    /// <summary>Reserved for the Billing module (Sprint 7); no foreign key until fee_plan exists.</summary>
    public Guid? FeePlanId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class StudentGroup : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public Guid RoomId { get; set; }

    public Room? Room { get; set; }
}

public class Label : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public string Color { get; set; } = "#E07A5F";
}

public class StudentLabel : ITenantEntity
{
    public Guid StudentId { get; set; }

    public Guid LabelId { get; set; }

    public Guid TenantId { get; set; }
}
