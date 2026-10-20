using NMS.Domain.Common;

namespace NMS.Domain.Forms;

public static class FormTypes
{
    public static readonly string[] All = ["enquiry", "admission", "check_in_out", "incident", "custom"];
}

public static class FormStatuses
{
    public const string Draft = "draft";
    public const string Live = "live";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Live, Archived];
}

public static class FieldTypes
{
    public static readonly string[] All = ["text", "textarea", "number", "date", "dropdown", "radio", "checkbox", "file", "signature", "section"];

    /// <summary>Fields that need a list of choices.</summary>
    public static readonly string[] WithOptions = ["dropdown", "radio"];

    /// <summary>Fields that only organise the form and collect nothing.</summary>
    public static bool CollectsAnswer(string type) => type != "section";
}

public sealed class FormOption
{
    public string Value { get; set; } = string.Empty;

    public Dictionary<string, string> Label { get; set; } = [];
}

/// <summary>One question (or section heading) on a form. Shown only when another answer matches, if a rule is set.</summary>
public sealed class FormField
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "text";

    public Dictionary<string, string> Label { get; set; } = [];

    public Dictionary<string, string>? Help { get; set; }

    public bool Required { get; set; }

    public List<FormOption> Options { get; set; } = [];

    public string? ShowWhenFieldId { get; set; }

    public string? ShowWhenEquals { get; set; }
}

/// <summary>A form the centre builds (admission, incident, check-in...). What people see is the published version.</summary>
public class Form : TenantEntity
{
    public Dictionary<string, string> Name { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }

    public string Type { get; set; } = "custom";

    public string Status { get; set; } = FormStatuses.Draft;

    /// <summary>Public forms can be filled in without signing in, from the centre's website.</summary>
    public bool IsPublic { get; set; }

    /// <summary>The version people fill in; zero until the first publish.</summary>
    public int PublishedVersion { get; set; }
}

/// <summary>The fields of a form at one point in time. Editing a published form starts a new draft version; the old one stays so past answers still make sense.</summary>
public class FormVersion : TenantEntity
{
    public Guid FormId { get; set; }

    public int Version { get; set; }

    public string FieldsJson { get; set; } = "[]";

    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>Someone opening a form. Lets the centre see how many start, how many finish, and where people give up.</summary>
public class FormStart : TenantEntity
{
    public Guid FormId { get; set; }

    public int Version { get; set; }

    public string? LastFieldId { get; set; }

    public bool Submitted { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
}

public class FormSubmission : TenantEntity
{
    public Guid FormId { get; set; }

    public int Version { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? EnquiryId { get; set; }

    public Guid? StartId { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    /// <summary>The answers as JSON, encrypted in the database because forms can hold personal details.</summary>
    [Sensitive]
    public string AnswersJson { get; set; } = "{}";

    public int? DurationSeconds { get; set; }
}
