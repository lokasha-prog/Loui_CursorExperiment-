using NMS.Domain.Common;
using NMS.Shared.Results;

namespace NMS.Domain.CentreManagement;

public static class StudentStatuses
{
    public const string SignedUp = "signed_up";
    public const string Active = "active";
    public const string OnHold = "on_hold";
    public const string Inactive = "inactive";
    public const string Withdrawn = "withdrawn";
    public const string Transferred = "transferred";

    public static readonly IReadOnlyList<string> All =
        [SignedUp, Active, OnHold, Inactive, Withdrawn, Transferred];

    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [SignedUp] = [Active, Withdrawn],
        [Active] = [OnHold, Inactive, Withdrawn, Transferred],
        [OnHold] = [Active, Inactive, Withdrawn],
        [Inactive] = [Active, Withdrawn],
        [Withdrawn] = [],
        [Transferred] = [],
    };

    public static bool CanTransition(string from, string to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}

public class Student : TenantEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Dictionary<string, string>? DisplayName { get; set; }

    public string? FirstNameArabic { get; set; }

    public string? LastNameArabic { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? Gender { get; set; }

    /// <summary>Reserved for the Media module (Sprint 3); no foreign key until media exists.</summary>
    public Guid? PhotoMediaId { get; set; }

    /// <summary>Photo of the child's identity card or passport page, visible to staff who manage students only.</summary>
    public Guid? IdPhotoMediaId { get; set; }

    public Guid? RoomId { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? ProgramId { get; set; }

    public Guid? KeyPersonStaffId { get; set; }

    public string Status { get; set; } = StudentStatuses.SignedUp;

    public DateTimeOffset StatusChangedAt { get; set; }

    public string? StatusReason { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Barcode { get; set; } = string.Empty;

    [Sensitive]
    public string? MedicalNotes { get; set; }

    [Sensitive]
    public string? AllergyInfo { get; set; }

    [Sensitive]
    public string? DietaryRequirements { get; set; }

    [Sensitive]
    public string? CustodyNotes { get; set; }

    [Sensitive]
    public string? PassportNumber { get; set; }

    [Sensitive]
    public string? EmiratesId { get; set; }

    public string? Nationality { get; set; }

    public string? Religion { get; set; }

    public string? PrimaryLanguage { get; set; }

    public List<StudentGuardian> StudentGuardians { get; set; } = [];

    public List<StudentLabel> Labels { get; set; } = [];

    public Result TransitionTo(string newStatus, string? reason, DateTimeOffset now)
    {
        if (!StudentStatuses.All.Contains(newStatus))
        {
            return Result.Failure(Error.Validation($"Unknown status '{newStatus}'.", "status"));
        }

        if (!StudentStatuses.CanTransition(Status, newStatus))
        {
            return Result.Failure(Error.InvalidState($"A student cannot move from '{Status}' to '{newStatus}'."));
        }

        Status = newStatus;
        StatusReason = reason;
        StatusChangedAt = now;
        if (newStatus is StudentStatuses.Withdrawn or StudentStatuses.Transferred)
        {
            EndDate = DateOnly.FromDateTime(now.UtcDateTime);
        }

        if (newStatus == StudentStatuses.Active && StartDate is null)
        {
            StartDate = DateOnly.FromDateTime(now.UtcDateTime);
        }

        return Result.Success();
    }
}
