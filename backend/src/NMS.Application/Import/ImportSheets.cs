using NMS.Application.Security;
using NMS.Domain.Admissions;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Import;

/// <summary>One column of an import sheet. <paramref name="Header"/> is what people see (a * marks a required column).</summary>
public sealed record ImportColumn(string Key, string Header, bool Required, string Hint, IReadOnlyList<string>? Choices = null);

/// <summary>One sheet of the import workbook: the kind of data, who may import it, and its columns.</summary>
public sealed record ImportSheet(string Name, string Purpose, string Permission, IReadOnlyList<ImportColumn> Columns);

/// <summary>A filled-in row as read from the workbook, keyed by the column header text.</summary>
public sealed record WorkbookRow(int Number, IReadOnlyDictionary<string, string> Cells);

public sealed record WorkbookSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<WorkbookRow> Rows);

/// <summary>Writes the sample workbook and reads a filled-in one (Excel .xlsx). Implemented in Infrastructure.</summary>
public interface IImportWorkbook
{
    byte[] BuildTemplate(IReadOnlyList<ImportSheet> sheets, IReadOnlyList<string> readMe);

    Result<IReadOnlyList<WorkbookSheet>> Read(byte[] file);
}

/// <summary>
/// The sheets a nursery moving from another system can fill in, in the order they are imported:
/// later sheets may refer to rooms, groups and children from earlier sheets of the same file.
/// </summary>
public static class ImportSheets
{
    public const string Rooms = "Rooms";
    public const string Groups = "Groups";
    public const string Staff = "Staff";
    public const string Children = "Children";
    public const string Enquiries = "Enquiries";
    public const string Balances = "Opening balances";

    private static readonly string[] Genders = ["female", "male"];

    public static readonly IReadOnlyList<ImportSheet> All =
    [
        new(Rooms, "Your rooms or classes.", Permissions.CentreStructureWrite,
        [
            new("name_en", "Room name (English) *", true, "For example Little Stars. Must be unique."),
            new("name_ar", "Room name (Arabic)", false, "Optional Arabic name."),
            new("capacity", "Capacity *", true, "Most children the room can take, a whole number."),
            new("min_age", "Youngest age in months *", true, "For example 12 for one year old."),
            new("max_age", "Oldest age in months *", true, "For example 35 for under three."),
            new("ratio", "Children per adult *", true, "For 1 adult to 4 children enter 4."),
        ]),
        new(Groups, "Groups inside a room (optional).", Permissions.CentreStructureWrite,
        [
            new("name_en", "Group name (English) *", true, "For example Busy Bees."),
            new("name_ar", "Group name (Arabic)", false, "Optional Arabic name."),
            new("room", "Room *", true, "A room from the Rooms sheet or already in the system."),
        ]),
        new(Staff, "Your team. Sign-in accounts are created later from User accounts.", Permissions.StaffWrite,
        [
            new("first_name", "First name *", true, "As on their documents."),
            new("last_name", "Last name *", true, ""),
            new("email", "Email *", true, "Must be unique: staff are matched by email when you upload again."),
            new("phone", "Phone", false, "With country code, for example +971501234567."),
            new("job_title", "Job title", false, "For example Lead teacher."),
            new("gender", "Gender", false, "female or male.", Genders),
            new("date_of_birth", "Date of birth", false, "YYYY-MM-DD."),
            new("hire_date", "Start date", false, "YYYY-MM-DD. Empty means today."),
            new("rooms", "Rooms", false, "Rooms they work in, separated by commas. They are rostered on your working days, 07:30 to 16:30."),
        ]),
        new(Children, "Children with up to two parents or guardians.", Permissions.StudentsWrite,
        [
            new("first_name", "First name *", true, ""),
            new("last_name", "Last name *", true, "Children are matched by first name, last name and date of birth when you upload again."),
            new("first_name_ar", "First name (Arabic)", false, ""),
            new("last_name_ar", "Last name (Arabic)", false, ""),
            new("date_of_birth", "Date of birth *", true, "YYYY-MM-DD."),
            new("gender", "Gender *", true, "female or male.", Genders),
            new("nationality", "Nationality code", false, "Two-letter country code, for example AE, IN, GB."),
            new("room", "Room", false, "A room from the Rooms sheet or already in the system. The child's age must fit the room."),
            new("group", "Group", false, "A group in that room."),
            new("start_date", "Start date", false, "YYYY-MM-DD."),
            new("status", "Status", false, "active (default) or signed_up for children who have not started yet.", [StudentStatuses.Active, StudentStatuses.SignedUp]),
            new("language", "Home language", false, "For example Arabic."),
            new("allergies", "Allergies", false, "Stored encrypted."),
            new("medical", "Medical notes", false, "Stored encrypted."),
            new("dietary", "Dietary needs", false, ""),
            new("g1_first_name", "Guardian 1 first name", false, "Fill all Guardian 1 columns, or none."),
            new("g1_last_name", "Guardian 1 last name", false, ""),
            new("g1_relationship", "Guardian 1 relationship", false, "mother, father, grandparent, legal_guardian or other.", GuardianRelationships.All),
            new("g1_email", "Guardian 1 email", false, "Used later to invite them to the parent app."),
            new("g1_phone", "Guardian 1 phone", false, ""),
            new("g2_first_name", "Guardian 2 first name", false, "Optional second guardian."),
            new("g2_last_name", "Guardian 2 last name", false, ""),
            new("g2_relationship", "Guardian 2 relationship", false, "", GuardianRelationships.All),
            new("g2_email", "Guardian 2 email", false, ""),
            new("g2_phone", "Guardian 2 phone", false, ""),
        ]),
        new(Enquiries, "Families still deciding (your enquiry pipeline).", Permissions.EnquiriesWrite,
        [
            new("family_name", "Family name *", true, ""),
            new("child_name", "Child name *", true, "Enquiries are matched by family and child name when you upload again."),
            new("phone", "Phone", false, "A phone or an email is needed."),
            new("email", "Email", false, ""),
            new("child_date_of_birth", "Child date of birth", false, "YYYY-MM-DD."),
            new("desired_start", "Wanted start date", false, "YYYY-MM-DD."),
            new("source", "Source *", true, "How they found you.", EnquirySources.All),
            new("stage", "Stage", false, "Where they are in the pipeline.", EnquiryStages.All.Where(s => s != EnquiryStages.Enrolled).ToList()),
            new("notes", "Notes", false, ""),
        ]),
        new(Balances, "Money each child's family still owes from your old system. Each row becomes an invoice.", Permissions.BillingWrite,
        [
            new("first_name", "Child first name *", true, "The child must be on the Children sheet or already in the system."),
            new("last_name", "Child last name *", true, ""),
            new("date_of_birth", "Child date of birth *", true, "YYYY-MM-DD, to tell children with the same name apart."),
            new("amount", "Amount owed (AED) *", true, "For example 2500 or 2500.50."),
            new("due_date", "Due date *", true, "YYYY-MM-DD."),
            new("description", "Description", false, "Empty means Opening balance from previous system."),
        ]),
    ];

    public static IReadOnlyList<string> ReadMe { get; } =
    [
        "Bright Futures: import your data",
        "1. Fill in only the sheets you need. Leave the others empty, but do not delete or rename sheets or column headers.",
        "2. Columns marked * are required. Dates are YYYY-MM-DD (for example 2025-03-14). Where a cell has a list, choose from it.",
        "3. Sheets are imported in this order: Rooms, Groups, Staff, Children, Enquiries, Opening balances, so later sheets can use rooms and children from earlier ones.",
        "4. Upload the file. It is checked first and nothing is saved: you see every row with a problem. Fix the file and upload it again, or import the rows that are ready.",
        "5. Uploading the same file again is safe: rows already in the system are recognised and skipped, never copied twice.",
        "Each sheet's columns are explained below.",
    ];
}
