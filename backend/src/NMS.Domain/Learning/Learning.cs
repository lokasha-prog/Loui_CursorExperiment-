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
}
