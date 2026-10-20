using NMS.Domain.Common;

namespace NMS.Domain.Auditing;

public static class AuditActions
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Restore = "restore";
    public const string Export = "export";
    public const string Login = "login";
    public const string Logout = "logout";
    public const string PermissionChange = "permission_change";
}

/// <summary>Append-only record of every mutation. UPDATE and DELETE are rejected by a database trigger.</summary>
public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string? UserEmail { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public Dictionary<string, AuditFieldChange>? ChangedFields { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? RequestId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public sealed record AuditFieldChange(object? Before, object? After);
