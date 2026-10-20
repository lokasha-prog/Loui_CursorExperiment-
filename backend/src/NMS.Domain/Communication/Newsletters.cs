using NMS.Domain.Common;

namespace NMS.Domain.Communication;

public static class NewsletterStatuses
{
    public const string Draft = "draft";
    public const string Scheduled = "scheduled";
    public const string Published = "published";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Scheduled, Published, Archived];
}

public static class NewsletterAudiences
{
    public const string All = "all";
    public const string Room = "room";
    public const string Program = "program";
    public const string Group = "group";
    public const string Label = "label";

    public static readonly string[] Kinds = [All, Room, Program, Group, Label];
}

/// <summary>A newsletter to parents, written in English and Arabic. The body is cleaned of anything unsafe before it is stored.</summary>
public class Newsletter : TenantEntity
{
    public Dictionary<string, string> Title { get; set; } = [];

    /// <summary>Safe HTML per language.</summary>
    public Dictionary<string, string> Body { get; set; } = [];

    public string AudienceType { get; set; } = NewsletterAudiences.All;

    public List<Guid> AudienceIds { get; set; } = [];

    public string Status { get; set; } = NewsletterStatuses.Draft;

    public DateTimeOffset? ScheduledFor { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>One parent who was sent the newsletter, with the private token that identifies them when they open it or click a link.</summary>
public class NewsletterRecipient : TenantEntity
{
    public Guid NewsletterId { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTimeOffset? OpenedAt { get; set; }
}

/// <summary>A link in a published newsletter. Clicks go through the centre first so they can be counted; only these saved addresses can ever be opened.</summary>
public class NewsletterLink : TenantEntity
{
    public Guid NewsletterId { get; set; }

    public int LinkIndex { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class NewsletterClick : TenantEntity
{
    public Guid NewsletterId { get; set; }

    public Guid RecipientId { get; set; }

    public int LinkIndex { get; set; }
}
