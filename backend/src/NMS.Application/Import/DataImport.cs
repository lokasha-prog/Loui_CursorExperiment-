using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Billing;
using NMS.Application.Enquiries;
using NMS.Application.Groups;
using NMS.Application.Rooms;
using NMS.Application.StaffMembers;
using NMS.Application.Attendance;
using NMS.Application.Students;
using NMS.Domain.Admissions;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Import;

public sealed record ImportProblem(int Row, string? Column, string Message);

/// <summary>What happened to one sheet: rows read, rows ready (or imported), rows already in the system, and the problems.</summary>
public sealed record ImportSheetReport(string Sheet, int Rows, int Ready, int AlreadyThere, IReadOnlyList<ImportProblem> Problems);

public sealed record ImportReportDto(bool Saved, int Ready, int AlreadyThere, int Problems, IReadOnlyList<ImportSheetReport> Sheets);

public sealed record ImportTemplateQuery : IRequest<Result<byte[]>>;

/// <summary>Checks (Save = false: nothing is kept) or imports (Save = true: rows without problems are kept) a filled-in workbook.</summary>
public sealed record RunImportCommand(byte[] File, bool Save) : IRequest<Result<ImportReportDto>>;

public sealed class ImportTemplateHandler(IImportWorkbook workbook) : IRequestHandler<ImportTemplateQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(ImportTemplateQuery request, CancellationToken ct) =>
        Task.FromResult(Result<byte[]>.Success(workbook.BuildTemplate(ImportSheets.All, ImportSheets.ReadMe)));
}

/// <summary>
/// Every row goes through the same commands as the screens (same checks, encryption, numbering and audit trail), all in one
/// database transaction. A check rolls the transaction back, so it finds exactly the problems a real import would meet,
/// including rooms that do not fit a child's age or emails already in use; an import commits the rows that worked.
/// </summary>
public sealed class RunImportHandler(IAppDbContext db, ISender sender, ICurrentUser current, ITenantContext tenant, IImportWorkbook workbook, CentreCalendar calendar)
    : IRequestHandler<RunImportCommand, Result<ImportReportDto>>
{
    public const int MaxRows = 5000;
    private const string OpeningBalance = "Opening balance from previous system";

    private IAppDbContext Db => db;

    private ISender Sender => sender;

    private ICurrentUser Current => current;

    private ITenantContext Tenant => tenant;

    private CentreCalendar Calendar => calendar;

    public async Task<Result<ImportReportDto>> Handle(RunImportCommand request, CancellationToken ct)
    {
        var read = workbook.Read(request.File);
        if (read.IsFailure)
        {
            return Result<ImportReportDto>.Failure(read.Errors);
        }

        var sheets = read.Value;
        if (sheets.Sum(s => s.Rows.Count) > MaxRows)
        {
            return Error.Validation($"The file has more than {MaxRows} rows. Split it into smaller files.", "file");
        }

        var known = ImportSheets.All.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!sheets.Any(s => known.Contains(s.Name)))
        {
            return Error.Validation("None of the sheets in this file are import sheets. Download the sample and fill that in.", "file");
        }

        var report = await db.InTransactionAsync(async token =>
        {
            var run = new Run(this, request.Save);
            await run.LoadAsync(token);
            var reports = new List<ImportSheetReport>();
            foreach (var definition in ImportSheets.All)
            {
                var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
                if (sheet is not null && sheet.Rows.Count > 0)
                {
                    reports.Add(await run.SheetAsync(definition, sheet, token));
                }
            }

            var result = new ImportReportDto(
                request.Save && reports.Sum(r => r.Ready) > 0, reports.Sum(r => r.Ready), reports.Sum(r => r.AlreadyThere),
                reports.Sum(r => r.Problems.Count), reports);
            return (result, result.Saved);
        }, ct);

        return Result<ImportReportDto>.Success(report);
    }

    /// <summary>One check or import: what is already in the system, plus what earlier rows of the file added.</summary>
    private sealed class Run(RunImportHandler h, bool save)
    {
        private readonly Dictionary<string, Guid> rooms = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(Guid Room, string Name), Guid> groups = [];
        private readonly HashSet<string> staffEmails = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string First, string Last, DateOnly Born), Guid> children = [];
        private readonly HashSet<(string Family, string Child)> enquiries = [];
        private int[] workDays = [];

        public async Task LoadAsync(CancellationToken ct)
        {
            foreach (var room in await h.Db.Rooms.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(ct))
            {
                foreach (var name in room.Name.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
                {
                    rooms.TryAdd(name.Trim(), room.Id);
                }
            }

            foreach (var group in await h.Db.StudentGroups.AsNoTracking().Select(g => new { g.Id, g.RoomId, g.Name }).ToListAsync(ct))
            {
                foreach (var name in group.Name.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
                {
                    groups.TryAdd((group.RoomId, Key(name)), group.Id);
                }
            }

            staffEmails.UnionWith(await h.Db.Staff.AsNoTracking().Select(s => s.Email).ToListAsync(ct));
            foreach (var child in await h.Db.Students.AsNoTracking().Select(s => new { s.Id, s.FirstName, s.LastName, s.DateOfBirth }).ToListAsync(ct))
            {
                children.TryAdd((Key(child.FirstName), Key(child.LastName), child.DateOfBirth), child.Id);
            }

            enquiries.UnionWith((await h.Db.Enquiries.AsNoTracking().Where(e => !e.IsArchived).Select(e => new { e.FamilyName, e.ChildName }).ToListAsync(ct))
                .Select(e => (Key(e.FamilyName), Key(e.ChildName))));

            var weekend = await h.Db.Tenants.AsNoTracking().Where(t => t.Id == h.Tenant.TenantId).Select(t => t.WeekendDays).FirstOrDefaultAsync(ct) ?? [];
            string[] days = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
            workDays = Enumerable.Range(0, 7).Where(d => !weekend.Contains(days[d], StringComparer.OrdinalIgnoreCase)).ToArray();
        }

        public async Task<ImportSheetReport> SheetAsync(ImportSheet definition, WorkbookSheet sheet, CancellationToken ct)
        {
            var problems = new List<ImportProblem>();
            int ready = 0, already = 0;
            if (!h.Current.HasPermission(definition.Permission))
            {
                problems.Add(new(0, null, "You do not have permission to add this kind of data."));
                return new(definition.Name, sheet.Rows.Count, 0, 0, problems);
            }

            // Match the file's headers to the sheet's columns, ignoring case, spaces and the * marker.
            var columns = new Dictionary<string, string>();
            foreach (var header in sheet.Headers)
            {
                var column = definition.Columns.FirstOrDefault(c => Header(c.Header) == Header(header));
                if (column is null)
                {
                    problems.Add(new(1, header, "This column is not part of the sample and was ignored."));
                }
                else
                {
                    columns[column.Key] = header;
                }
            }

            var missing = definition.Columns.Where(c => c.Required && !columns.ContainsKey(c.Key)).ToList();
            if (missing.Count > 0)
            {
                problems.Add(new(1, null, $"Required column missing: {string.Join(", ", missing.Select(c => c.Header.TrimEnd(' ', '*')))}. Use the headers from the sample."));
                return new(definition.Name, sheet.Rows.Count, 0, 0, problems);
            }

            foreach (var row in sheet.Rows)
            {
                var cells = new Cells(definition, columns, row);
                var outcome = cells.Problems.Count > 0 ? Outcome.Problem : await RowAsync(definition.Name, cells, ct);
                if (outcome == Outcome.Problem)
                {
                    // Nothing half-done from a failed row may reach the next row's save.
                    h.Db.ForgetPendingChanges();
                }

                problems.AddRange(cells.Problems);
                if (outcome == Outcome.Ready)
                {
                    ready++;
                }
                else if (outcome == Outcome.AlreadyThere)
                {
                    already++;
                }
            }

            return new(definition.Name, sheet.Rows.Count, ready, already, problems);
        }

        private Task<Outcome> RowAsync(string sheet, Cells c, CancellationToken ct) => sheet switch
        {
            ImportSheets.Rooms => RoomAsync(c, ct),
            ImportSheets.Groups => GroupAsync(c, ct),
            ImportSheets.Staff => StaffAsync(c, ct),
            ImportSheets.Children => ChildAsync(c, ct),
            ImportSheets.Enquiries => EnquiryAsync(c, ct),
            _ => BalanceAsync(c, ct),
        };

        private async Task<Outcome> RoomAsync(Cells c, CancellationToken ct)
        {
            var name = c.Text("name_en")!;
            int? capacity = c.Int("capacity"), min = c.Int("min_age"), max = c.Int("max_age"), ratio = c.Int("ratio");
            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (rooms.ContainsKey(name))
            {
                return Outcome.AlreadyThere;
            }

            var created = await h.Sender.Send(new CreateRoomCommand(Localized(name, c.Text("name_ar")), capacity!.Value, min!.Value, max!.Value, ratio!.Value), ct);
            if (!c.Accept(created, ("capacity", "capacity"), ("min_age_months", "min_age"), ("max_age_months", "max_age"), ("ratio_staff_to_student", "ratio")))
            {
                return Outcome.Problem;
            }

            rooms[name] = created.Value.Id;
            return Outcome.Ready;
        }

        private async Task<Outcome> GroupAsync(Cells c, CancellationToken ct)
        {
            var name = c.Text("name_en")!;
            var room = c.Room("room", rooms);
            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (groups.ContainsKey((room!.Value, Key(name))))
            {
                return Outcome.AlreadyThere;
            }

            var created = await h.Sender.Send(new CreateGroupCommand(Localized(name, c.Text("name_ar")), room.Value), ct);
            if (!c.Accept(created))
            {
                return Outcome.Problem;
            }

            groups[(room.Value, Key(name))] = created.Value.Id;
            return Outcome.Ready;
        }

        private async Task<Outcome> StaffAsync(Cells c, CancellationToken ct)
        {
            var email = c.Text("email")!.ToLowerInvariant();
            var born = c.Date("date_of_birth");
            var hired = c.Date("hire_date") ?? h.Calendar.Today;
            var gender = c.Choice("gender");
            var roomIds = (c.Text("rooms") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => c.RoomName("rooms", r, rooms)).ToList();
            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (staffEmails.Contains(email))
            {
                return Outcome.AlreadyThere;
            }

            var created = await h.Sender.Send(new CreateStaffCommand(new StaffFields(
                c.Text("first_name")!, c.Text("last_name")!, born, gender, email, c.Text("phone"), c.Text("job_title"), hired, null, "active", null, null, null)), ct);
            if (!c.Accept(created, ("email", "email"), ("phone", "phone"), ("date_of_birth", "date_of_birth"), ("hire_date", "hire_date")))
            {
                return Outcome.Problem;
            }

            foreach (var room in roomIds.Distinct())
            {
                foreach (var day in workDays)
                {
                    var shift = await h.Sender.Send(new CreateRoomAssignmentCommand(created.Value.Id, room!.Value, day, new TimeOnly(7, 30), new TimeOnly(16, 30), hired, null), ct);
                    if (!c.Accept(shift, ("room_id", "rooms")))
                    {
                        return Outcome.Problem;
                    }
                }
            }

            staffEmails.Add(email);
            return Outcome.Ready;
        }

        private async Task<Outcome> ChildAsync(Cells c, CancellationToken ct)
        {
            var first = c.Text("first_name")!;
            var last = c.Text("last_name")!;
            var born = c.Date("date_of_birth");
            var gender = c.Choice("gender");
            var status = c.Choice("status") ?? StudentStatuses.Active;
            var start = c.Date("start_date");
            var nationality = c.Text("nationality")?.ToUpperInvariant();
            if (nationality is not null && nationality.Length != 2)
            {
                c.Problem("nationality", "Use the two-letter country code, for example AE.");
            }

            var room = c.Text("room") is null ? null : c.Room("room", rooms);
            Guid? group = null;
            if (c.Text("group") is { } groupName)
            {
                if (room is null)
                {
                    c.Problem("group", "Give the room as well, so the group can be found.");
                }
                else if (groups.TryGetValue((room.Value, Key(groupName)), out var found))
                {
                    group = found;
                }
                else
                {
                    c.Problem("group", $"There is no group \"{groupName}\" in that room.");
                }
            }

            var guardians = new List<NewGuardianInput>();
            foreach (var g in new[] { "g1", "g2" })
            {
                var fields = new[] { $"{g}_first_name", $"{g}_last_name", $"{g}_relationship", $"{g}_email", $"{g}_phone" };
                if (fields.All(f => c.Text(f) is null))
                {
                    continue;
                }

                foreach (var needed in fields.Take(4).Where(f => c.Text(f) is null))
                {
                    c.Problem(needed, "Needed when any guardian column is filled in.");
                }

                var relationship = c.Choice($"{g}_relationship");
                if (!c.Failed)
                {
                    guardians.Add(new NewGuardianInput(c.Text($"{g}_first_name")!, c.Text($"{g}_last_name")!, c.Text($"{g}_email")!.ToLowerInvariant(),
                        c.Text($"{g}_phone"), relationship!, IsPrimary: g == "g1"));
                }
            }

            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (children.ContainsKey((Key(first), Key(last), born!.Value)))
            {
                return Outcome.AlreadyThere;
            }

            var created = await h.Sender.Send(new CreateStudentCommand(new StudentFields(
                first, last, c.Text("first_name_ar"), c.Text("last_name_ar"), born.Value, gender, room, group, null, null, start, nationality, null,
                c.Text("language"), c.Text("medical"), c.Text("allergies"), c.Text("dietary"), null, null, null, null), guardians), ct);
            if (!c.Accept(created, ("room_id", "room"), ("group_id", "group"), ("date_of_birth", "date_of_birth"), ("nationality", "nationality"), ("gender", "gender")))
            {
                return Outcome.Problem;
            }

            if (status == StudentStatuses.Active && created.Value.Status != StudentStatuses.Active
                && !c.Accept(await h.Sender.Send(new TransitionStudentStatusCommand(created.Value.Id, StudentStatuses.Active, "Imported from previous system"), ct), ("status", "status")))
            {
                return Outcome.Problem;
            }

            children[(Key(first), Key(last), born.Value)] = created.Value.Id;
            return Outcome.Ready;
        }

        private async Task<Outcome> EnquiryAsync(Cells c, CancellationToken ct)
        {
            var family = c.Text("family_name")!;
            var child = c.Text("child_name")!;
            var source = c.Choice("source");
            var stage = c.Choice("stage");
            var born = c.Date("child_date_of_birth");
            var start = c.Date("desired_start");
            if (c.Text("phone") is null && c.Text("email") is null)
            {
                c.Problem("phone", "Give a phone number or an email.");
            }

            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (enquiries.Contains((Key(family), Key(child))))
            {
                return Outcome.AlreadyThere;
            }

            var created = await h.Sender.Send(new SaveEnquiryCommand(null, new EnquiryFields(
                family, child, c.Text("phone"), c.Text("email")?.ToLowerInvariant(), null, born, start, null, source!, null, c.Text("notes"))), ct);
            if (!c.Accept(created, ("phone", "phone"), ("email", "email"), ("source", "source")))
            {
                return Outcome.Problem;
            }

            if (stage is not null && stage != EnquiryStages.NoStage
                && !c.Accept(await h.Sender.Send(new ChangeStageCommand(created.Value.Enquiry.Id, stage, "Imported from previous system"), ct), ("stage", "stage")))
            {
                return Outcome.Problem;
            }

            enquiries.Add((Key(family), Key(child)));
            return Outcome.Ready;
        }

        private async Task<Outcome> BalanceAsync(Cells c, CancellationToken ct)
        {
            var born = c.Date("date_of_birth");
            var amount = c.Money("amount");
            var due = c.Date("due_date");
            var description = c.Text("description") ?? OpeningBalance;
            if (c.Failed)
            {
                return Outcome.Problem;
            }

            if (!children.TryGetValue((Key(c.Text("first_name")!), Key(c.Text("last_name")!), born!.Value), out var studentId))
            {
                c.Problem("first_name", "No child with this name and date of birth, on the Children sheet or in the system.");
                return Outcome.Problem;
            }

            var fils = amount!.Value;
            var existing = await h.Db.Invoices.AsNoTracking().Where(i => i.StudentId == studentId && i.TotalFils == fils).Select(i => i.Id).ToListAsync(ct);
            var lines = await h.Db.InvoiceLines.AsNoTracking().Where(l => existing.Contains(l.InvoiceId)).Select(l => l.Description).ToListAsync(ct);
            if (lines.Any(d => d.TryGetValue("en", out var en) && en == description))
            {
                return Outcome.AlreadyThere;
            }

            // A check leaves the invoice as a draft, so families are not told about an invoice that is then rolled back.
            var created = await h.Sender.Send(new CreateInvoiceCommand(
                studentId, new DateOnly(due!.Value.Year, due.Value.Month, 1), due.Value, null, due.Value,
                [new LineInput(new Dictionary<string, string> { ["en"] = description, ["ar"] = "رصيد افتتاحي من النظام السابق" }, 1, fils, null)],
                new Dictionary<string, string> { ["en"] = "Imported from previous system" }, Issue: save), ct);
            return c.Accept(created, ("due_date", "due_date")) ? Outcome.Ready : Outcome.Problem;
        }
    }

    private enum Outcome
    {
        Ready,
        AlreadyThere,
        Problem,
    }

    private static string Key(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Header(string value) => new(value.Replace("*", string.Empty).Where(ch => !char.IsWhiteSpace(ch)).Select(char.ToLowerInvariant).ToArray());

    private static Dictionary<string, string> Localized(string en, string? ar) =>
        ar is null ? new() { ["en"] = en } : new() { ["en"] = en, ["ar"] = ar };

    /// <summary>One row's cells, with typed reading that records a problem against the column when a value is wrong.</summary>
    private sealed class Cells
    {
        private readonly ImportSheet sheet;
        private readonly Dictionary<string, string> columns;
        private readonly WorkbookRow row;

        public Cells(ImportSheet sheet, Dictionary<string, string> columns, WorkbookRow row)
        {
            this.sheet = sheet;
            this.columns = columns;
            this.row = row;
            foreach (var column in sheet.Columns.Where(c => c.Required && Text(c.Key) is null))
            {
                Problem(column.Key, "Required.");
            }
        }

        public List<ImportProblem> Problems { get; } = [];

        public bool Failed => Problems.Count > 0;

        public string? Text(string key) =>
            columns.TryGetValue(key, out var header) && row.Cells.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        public void Problem(string? key, string message)
        {
            var header = key is null ? null : sheet.Columns.FirstOrDefault(c => c.Key == key)?.Header.TrimEnd(' ', '*');
            Problems.Add(new ImportProblem(row.Number, header, message));
        }

        public int? Int(string key)
        {
            var text = Text(key);
            if (text is null)
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == Math.Floor(number) && number >= 0)
            {
                return (int)number;
            }

            Problem(key, $"\"{text}\" is not a whole number.");
            return null;
        }

        public long? Money(string key)
        {
            var text = Text(key)?.Replace("AED", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(",", string.Empty).Trim();
            if (text is null)
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var aed) && aed > 0 && decimal.Round(aed, 2) == aed)
            {
                return (long)(aed * 100);
            }

            Problem(key, $"\"{text}\" is not an amount above zero (for example 2500.50).");
            return null;
        }

        public DateOnly? Date(string key)
        {
            var text = Text(key);
            if (text is null)
            {
                return null;
            }

            string[] formats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy"];
            if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            Problem(key, $"\"{text}\" is not a date. Use YYYY-MM-DD.");
            return null;
        }

        public string? Choice(string key)
        {
            var text = Text(key)?.ToLowerInvariant().Replace(' ', '_');
            var choices = sheet.Columns.First(c => c.Key == key).Choices!;
            if (text is null || choices.Contains(text))
            {
                return text;
            }

            Problem(key, $"\"{Text(key)}\" is not one of: {string.Join(", ", choices)}.");
            return null;
        }

        public Guid? Room(string key, Dictionary<string, Guid> rooms) => RoomName(key, Text(key)!, rooms);

        public Guid? RoomName(string key, string name, Dictionary<string, Guid> rooms)
        {
            if (rooms.TryGetValue(name.Trim(), out var id))
            {
                return id;
            }

            Problem(key, $"There is no room \"{name}\". Add it on the Rooms sheet or check the spelling.");
            return null;
        }

        /// <summary>Records the command's own problems against the right columns; true when it worked.</summary>
        public bool Accept<T>(Result<T> result, params (string Field, string Column)[] fields)
        {
            if (result.IsSuccess)
            {
                return true;
            }

            foreach (var error in result.Errors)
            {
                var column = fields.FirstOrDefault(f => error.Field is not null && error.Field.EndsWith(f.Field, StringComparison.OrdinalIgnoreCase)).Column;
                Problem(column, error.Message);
            }

            return false;
        }
    }
}
