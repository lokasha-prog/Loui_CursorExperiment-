namespace NMS.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>Universal columns required on every persistent tenant table (see spec 07 §1.1).</summary>
public abstract class TenantEntity : Entity, ITenantEntity, IAuditable, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Localised text stored as JSONB, for example {"en": "Toddlers", "ar": "..."}.</summary>
public static class LocalizedText
{
    public static Dictionary<string, string> Create(string en, string? ar = null)
    {
        var text = new Dictionary<string, string> { ["en"] = en };
        if (!string.IsNullOrWhiteSpace(ar))
        {
            text["ar"] = ar;
        }

        return text;
    }
}
