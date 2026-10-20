namespace NMS.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }

    Guid CreatedBy { get; set; }

    Guid UpdatedBy { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Marks a property as sensitive: stored encrypted and masked in the audit log.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute;
