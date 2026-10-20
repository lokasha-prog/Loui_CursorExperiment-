using NMS.Domain.Common;

namespace NMS.Domain.Communication;

public static class ResourceAudiences
{
    public const string Parents = "parents";
    public const string Staff = "staff";

    public static readonly string[] All = [Parents, Staff];
}

/// <summary>A folder in the document library. Parent folders are for families and staff; staff folders are only for staff.</summary>
public class ResourceFolder : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public string Audience { get; set; } = ResourceAudiences.Parents;

    public Guid? ParentFolderId { get; set; }
}

public class ResourceDocument : TenantEntity
{
    public Guid FolderId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Comma-separated words that help people find it.</summary>
    public string? Tags { get; set; }

    public int CurrentVersion { get; set; } = 1;
}

/// <summary>One uploaded file of a document. Older versions are kept so nothing is ever lost.</summary>
public class ResourceVersion : TenantEntity
{
    public Guid DocumentId { get; set; }

    public int Version { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public byte[] Content { get; set; } = [];

    public Guid UploadedBy { get; set; }
}

/// <summary>Who downloaded which version, and when.</summary>
public class ResourceDownload : TenantEntity
{
    public Guid DocumentId { get; set; }

    public Guid VersionId { get; set; }

    public Guid UserId { get; set; }
}
