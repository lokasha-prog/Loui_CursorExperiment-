using NMS.Domain.Common;
using NMS.Domain.Identity;

namespace NMS.Domain.CentreManagement;

public static class GuardianRelationships
{
    public static readonly IReadOnlyList<string> All =
        ["mother", "father", "grandparent", "legal_guardian", "other"];
}

public class Guardian : TenantEntity
{
    /// <summary>Null until the guardian accepts their invitation and gets a login.</summary>
    public Guid? AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Dictionary<string, string>? DisplayName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Relationship { get; set; } = "other";

    public bool IsPrimaryContact { get; set; }

    public bool CanPickup { get; set; } = true;

    public bool ReceivesCommunications { get; set; } = true;

    public string? Nationality { get; set; }

    [Sensitive]
    public string? EmiratesId { get; set; }

    public string? Occupation { get; set; }

    public string? Employer { get; set; }
}

public class StudentGuardian : ITenantEntity
{
    public Guid StudentId { get; set; }

    public Guid GuardianId { get; set; }

    public Guid TenantId { get; set; }

    public bool IsPrimary { get; set; }

    public Student? Student { get; set; }

    public Guardian? Guardian { get; set; }
}
