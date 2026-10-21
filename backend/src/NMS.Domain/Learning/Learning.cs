using NMS.Domain.Common;

namespace NMS.Domain.Learning;

/// <summary>A curriculum the centre follows (EYFS, Montessori, or one it defines). Everything under it is the centre's own data, entered or imported, never built in.</summary>
public class CurriculumFramework : TenantEntity
{
    /// <summary>Short web-safe name such as "eyfs", unique in the centre.</summary>
    public string Code { get; set; } = string.Empty;

    public Dictionary<string, string> Name { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }
}

/// <summary>A development area of a framework, for example Communication and Language.</summary>
public class DevelopmentArea : TenantEntity
{
    public Guid FrameworkId { get; set; }

    public string Code { get; set; } = string.Empty;

    public Dictionary<string, string> Name { get; set; } = [];

    public int SortOrder { get; set; }
}

/// <summary>An age band in months. Bands may overlap: a child moves between them by development, not by birthday.</summary>
public class AgeBand : TenantEntity
{
    public Guid FrameworkId { get; set; }

    public string Code { get; set; } = string.Empty;

    public Dictionary<string, string> Name { get; set; } = [];

    public int MinMonths { get; set; }

    public int MaxMonths { get; set; }
}

/// <summary>One thing a child is expected to be able to do in an area at an age band.</summary>
public class Milestone : TenantEntity
{
    public Guid FrameworkId { get; set; }

    public Guid AreaId { get; set; }

    public Guid AgeBandId { get; set; }

    public string Code { get; set; } = string.Empty;

    public Dictionary<string, string> Statement { get; set; } = [];

    public int SortOrder { get; set; }
}

public class Lesson : TenantEntity
{
    public Dictionary<string, string> Title { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }

    public Guid FrameworkId { get; set; }

    public Guid? AreaId { get; set; }

    public Guid? AgeBandId { get; set; }

    /// <summary>When set, only staff of that room (and managers) see the lesson.</summary>
    public Guid? RoomId { get; set; }

    public int DurationMinutes { get; set; } = 30;

    public List<string> Materials { get; set; } = [];

    public List<string> Objectives { get; set; } = [];

    /// <summary>Web address to a title, for linked resources.</summary>
    public Dictionary<string, string> Links { get; set; } = [];

    public List<string> MilestoneIds { get; set; } = [];

    /// <summary>A reusable plan for an age group. Teachers copy it into their own lessons.</summary>
    public bool IsTemplate { get; set; }
}

public static class ObservationTypes
{
    public const string Formal = "formal";
    public const string Informal = "informal";
    public const string Concern = "concern";

    public static readonly string[] All = [Formal, Informal, Concern];
}

/// <summary>What a teacher saw a child do. Concerns and anything not marked "share" stay with staff; the journey shows parents only what was shared.</summary>
public class Observation : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid ObserverUserId { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public string Type { get; set; } = ObservationTypes.Informal;

    public Guid? FrameworkId { get; set; }

    public Guid? AreaId { get; set; }

    public Guid? AgeBandId { get; set; }

    public List<string> MilestoneIds { get; set; } = [];

    public Dictionary<string, string> Notes { get; set; } = [];

    public List<string> MediaIds { get; set; } = [];

    public bool ShareWithParents { get; set; }
}

public static class ScoringKinds
{
    public const string Binary = "binary";
    public const string ThreePoint = "three_point";

    public static readonly string[] All = [Binary, ThreePoint];
}

/// <summary>A reusable checklist of milestones for one age group, scored yes/no or on three levels.</summary>
public class AssessmentTemplate : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public Guid FrameworkId { get; set; }

    public Guid? AgeBandId { get; set; }

    public string ScoringKind { get; set; } = ScoringKinds.ThreePoint;

    public List<string> MilestoneIds { get; set; } = [];
}

public static class AssessmentStatuses
{
    public const string Draft = "draft";
    public const string Completed = "completed";

    public static readonly string[] All = [Draft, Completed];
}

/// <summary>One child assessed once (for example per term). The parent version is shown only after a manager approves it.</summary>
public class AssessmentRun : TenantEntity
{
    public Guid TemplateId { get; set; }

    public Guid StudentId { get; set; }

    public string Term { get; set; } = string.Empty;

    public DateOnly AssessedOn { get; set; }

    public string Status { get; set; } = AssessmentStatuses.Draft;

    public Dictionary<string, string>? StaffSummary { get; set; }

    public Dictionary<string, string>? ParentSummary { get; set; }

    public bool ParentApproved { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
}

/// <summary>Score for one milestone: binary 0/1, three-point 0 (not yet), 1 (emerging), 2 (secure).</summary>
public class AssessmentScore : TenantEntity
{
    public Guid RunId { get; set; }

    public Guid MilestoneId { get; set; }

    public int Score { get; set; }

    public string? Note { get; set; }
}
