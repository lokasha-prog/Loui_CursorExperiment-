using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Application.Security;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Domain.Forms;
using NMS.Domain.Identity;
using NMS.Domain.Notifications;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Reporting;

/// <summary>What a report looks like before it is drawn: a title, a few headline numbers and a table. Excel, PDF and JSON are all made from this.</summary>
public sealed record ReportTable(string Title, string? Subtitle, string Language, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyList<(string Label, string Value)> Summary);

public sealed record RenderedReport(byte[] Content, string ContentType, string Extension);

/// <summary>Draws a report as a file. Implemented outside this layer so the Excel and PDF libraries do not leak in here.</summary>
public interface IReportRenderer
{
    RenderedReport Render(ReportTable table, string format);
}

/// <summary>Hands a requested report to the background worker so people are never kept waiting.</summary>
public interface IReportQueue
{
    void Enqueue(Guid tenantId, Guid runId);
}

public sealed record ReportDefinition(string Kind, Dictionary<string, string> Name, IReadOnlyList<string> Formats, IReadOnlyList<string> Filters, string Permission);

public sealed record ReportRunDto(Guid Id, string Kind, string Format, string Status, string? Error, string? FileName, Dictionary<string, string?> Parameters, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset ExpiresAt) : ICursorItem;

public sealed record ListReportDefinitionsQuery : IRequest<Result<IReadOnlyList<ReportDefinition>>>;

public sealed record RequestReportCommand(string Kind, string Format, Dictionary<string, string?>? Parameters, string? Language) : IRequest<Result<ReportRunDto>>;

public sealed record ListReportRunsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<ReportRunDto>>>;

public sealed record DownloadReportQuery(Guid Id) : IRequest<Result<ReportDownload>>;

public sealed record ReportDownload(byte[] Content, string ContentType, string FileName);

public sealed record ProcessReportRunCommand(Guid RunId) : IRequest<Result>;

public sealed record RunReportMaintenanceCommand : IRequest<Result<int>>;

public static class ReportCatalogue
{
    public static readonly IReadOnlyList<ReportDefinition> All =
    [
        D("attendance_students", "Monthly attendance (children)", "الحضور الشهري (الأطفال)", ["excel", "pdf"], ["month", "room_id", "program_id"], Permissions.ReportsGenerate),
        D("attendance_staff", "Monthly attendance (staff)", "الحضور الشهري (الموظفون)", ["excel", "pdf"], ["month", "room_id"], Permissions.ReportsGenerate),
        D("billing_revenue", "Billing and revenue", "الفواتير والإيرادات", ["excel"], ["from", "to", "status"], Permissions.BillingRead),
        D("enquiry_conversion", "Enquiry conversion", "تحويل الاستفسارات", ["excel", "pdf"], ["from", "to", "source", "staff_id"], Permissions.EnquiriesRead),
        D("adoption_parents", "Parent app use", "استخدام الأهل للتطبيق", ["excel"], ["from", "to"], Permissions.ReportsGenerate),
        D("adoption_staff", "Staff app use", "استخدام الموظفين للتطبيق", ["excel"], ["from", "to"], Permissions.ReportsGenerate),
        D("audit_log", "Audit log export", "تصدير سجل التدقيق", ["excel", "json"], ["from", "to", "user_email", "entity_type", "action"], Permissions.AuditLogView),
        D("sign_in_sheet", "Daily sign-in sheet", "كشف الحضور اليومي", ["pdf"], ["date", "room_id"], Permissions.AttendanceRead),
    ];

    private static ReportDefinition D(string kind, string en, string ar, string[] formats, string[] filters, string permission) =>
        new(kind, new() { ["en"] = en, ["ar"] = ar }, formats, filters, permission);
}

public sealed class RequestReportValidator : AbstractValidator<RequestReportCommand>
{
    public RequestReportValidator()
    {
        RuleFor(x => x.Kind).Must(k => ReportCatalogue.All.Any(r => r.Kind == k)).WithMessage("Unknown report.");
        RuleFor(x => x).Must(x => ReportCatalogue.All.FirstOrDefault(r => r.Kind == x.Kind)?.Formats.Contains(x.Format) ?? true).WithMessage("That report is not available in that format.");
        RuleFor(x => x.Language).Must(l => l is null or "en" or "ar").WithMessage("Language must be en or ar.");
        RuleFor(x => x.Parameters).Custom((p, context) =>
        {
            var kind = context.InstanceToValidate.Kind;
            string? Get(string k) => p?.GetValueOrDefault(k);
            bool Month(string? m) => DateOnly.TryParseExact(m + "-01", "yyyy-MM-dd", out _);
            bool Date(string? d) => DateOnly.TryParseExact(d, "yyyy-MM-dd", out _);
            switch (kind)
            {
                case "attendance_students" or "attendance_staff":
                    if (!Month(Get("month"))) context.AddFailure("month", "Choose a month like 2026-10.");
                    break;
                case "sign_in_sheet":
                    if (Get("date") is { Length: > 0 } d && !Date(d)) context.AddFailure("date", "Choose a date.");
                    break;
                case "billing_revenue" or "enquiry_conversion" or "adoption_parents" or "adoption_staff" or "audit_log":
                    if (!Date(Get("from")) || !Date(Get("to")))
                    {
                        context.AddFailure("from", "Choose a start and end date.");
                    }
                    else if (DateOnly.ParseExact(Get("to")!, "yyyy-MM-dd") < DateOnly.ParseExact(Get("from")!, "yyyy-MM-dd") || DateOnly.ParseExact(Get("to")!, "yyyy-MM-dd").DayNumber - DateOnly.ParseExact(Get("from")!, "yyyy-MM-dd").DayNumber > 366)
                    {
                        context.AddFailure("to", "The range must be at most a year, and the end cannot be before the start.");
                    }

                    break;
            }

            foreach (var key in new[] { "room_id", "program_id", "staff_id" })
            {
                if (Get(key) is { Length: > 0 } g && !Guid.TryParse(g, out _)) context.AddFailure(key, "Not a valid choice.");
            }
        });
    }
}

public sealed class ListReportDefinitionsHandler(ICurrentUser current) : IRequestHandler<ListReportDefinitionsQuery, Result<IReadOnlyList<ReportDefinition>>>
{
    public Task<Result<IReadOnlyList<ReportDefinition>>> Handle(ListReportDefinitionsQuery request, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<ReportDefinition>>.Success(ReportCatalogue.All.Where(r => current.HasPermission(r.Permission)).ToList()));
}

internal static class RunMapping
{
    public static ReportRunDto ToDto(ReportRun r) => new(
        r.Id, r.Kind, r.Format, r.Status, r.Error, r.FileName,
        JsonSerializer.Deserialize<Dictionary<string, string?>>(r.ParametersJson) ?? [], r.CreatedAt, r.CompletedAt, r.ExpiresAt);
}

public sealed class RequestReportHandler(IAppDbContext db, ICurrentUser current, ITenantContext tenant, IClock clock, IReportQueue queue) : IRequestHandler<RequestReportCommand, Result<ReportRunDto>>
{
    public async Task<Result<ReportRunDto>> Handle(RequestReportCommand request, CancellationToken ct)
    {
        var definition = ReportCatalogue.All.First(r => r.Kind == request.Kind);
        if (!current.HasPermission(definition.Permission))
        {
            return Error.Forbidden("You do not have permission to run this report.");
        }

        var parameters = (request.Parameters ?? []).Where(p => definition.Filters.Contains(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => (string?)p.Value!.Trim());
        parameters["language"] = request.Language ?? "en";
        var run = new ReportRun
        {
            Kind = request.Kind, Format = request.Format, ParametersJson = JsonSerializer.Serialize(parameters), RequestedBy = current.UserId, ExpiresAt = clock.UtcNow.AddDays(30),
        };
        db.ReportRuns.Add(run);
        await db.SaveChangesAsync(ct);
        queue.Enqueue(tenant.TenantId, run.Id);
        return Result<ReportRunDto>.Success(RunMapping.ToDto(run));
    }
}

public sealed class ListReportRunsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListReportRunsQuery, Result<CursorPage<ReportRunDto>>>
{
    public async Task<Result<CursorPage<ReportRunDto>>> Handle(ListReportRunsQuery request, CancellationToken ct)
    {
        var q = db.ReportRuns.AsNoTracking().AsQueryable();
        if (!current.HasPermission(Permissions.ReportsGenerate) || !current.HasPermission(Permissions.SettingsRead))
        {
            q = q.Where(r => r.RequestedBy == current.UserId);
        }

        var page = await q.ToCursorPageAsync(request.Cursor, request.Limit,
            r => new ReportRunDto(r.Id, r.Kind, r.Format, r.Status, r.Error, r.FileName, new Dictionary<string, string?>(), r.CreatedAt, r.CompletedAt, r.ExpiresAt), ct);
        var ids = page.Items.Select(i => i.Id).ToList();
        var parameters = await db.ReportRuns.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new { r.Id, r.ParametersJson }).ToDictionaryAsync(r => r.Id, r => r.ParametersJson, ct);
        var items = page.Items.Select(i => i with { Parameters = JsonSerializer.Deserialize<Dictionary<string, string?>>(parameters.GetValueOrDefault(i.Id) ?? "{}") ?? [] }).ToList();
        return Result<CursorPage<ReportRunDto>>.Success(new CursorPage<ReportRunDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }
}

public sealed class DownloadReportHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<DownloadReportQuery, Result<ReportDownload>>
{
    public async Task<Result<ReportDownload>> Handle(DownloadReportQuery request, CancellationToken ct)
    {
        var run = await db.ReportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        var seesAll = current.HasPermission(Permissions.ReportsGenerate) && current.HasPermission(Permissions.SettingsRead);
        if (run is null || (!seesAll && run.RequestedBy != current.UserId))
        {
            return Error.NotFound("Report not found.");
        }

        return run.Status != ReportStatuses.Done || run.Content is null
            ? Error.InvalidState(run.Status == ReportStatuses.Failed ? "This report could not be made." : "This report is not ready yet.")
            : Result<ReportDownload>.Success(new ReportDownload(run.Content, run.ContentType ?? "application/octet-stream", run.FileName ?? "report"));
    }
}

/// <summary>The words used in report headings and columns, in both languages.</summary>
internal static class ReportText
{
    private static readonly Dictionary<string, (string En, string Ar)> Words = new()
    {
        ["child"] = ("Child", "الطفل"), ["room"] = ("Room", "الغرفة"), ["present"] = ("Present", "حاضر"), ["late"] = ("Late", "متأخر"), ["early"] = ("Left early", "غادر مبكراً"),
        ["absent"] = ("Absent", "غائب"), ["days"] = ("Days recorded", "الأيام المسجلة"), ["rate"] = ("Attendance %", "نسبة الحضور %"), ["staff"] = ("Staff", "الموظف"), ["job"] = ("Job title", "المسمى الوظيفي"),
        ["invoice"] = ("Invoice", "الفاتورة"), ["issued"] = ("Issued", "الإصدار"), ["due"] = ("Due", "الاستحقاق"), ["total"] = ("Total (AED)", "الإجمالي (درهم)"), ["paid"] = ("Paid (AED)", "المدفوع (درهم)"),
        ["balance"] = ("Balance (AED)", "المتبقي (درهم)"), ["status"] = ("Status", "الحالة"), ["billed"] = ("Billed (AED)", "المفوتر (درهم)"), ["collected"] = ("Collected (AED)", "المحصّل (درهم)"),
        ["outstanding"] = ("Outstanding (AED)", "المستحق (درهم)"), ["source"] = ("Source", "المصدر"), ["enquiries"] = ("Enquiries", "الاستفسارات"), ["toured"] = ("Toured", "زاروا"), ["enrolled"] = ("Enrolled", "سُجّلوا"),
        ["conversion"] = ("Conversion %", "نسبة التحويل %"), ["parent"] = ("Parent", "ولي الأمر"), ["children"] = ("Children", "الأطفال"), ["lastLogin"] = ("Last sign-in", "آخر دخول"), ["usedApp"] = ("Signed in", "سجّل الدخول"),
        ["yes"] = ("Yes", "نعم"), ["no"] = ("No", "لا"), ["when"] = ("When", "الوقت"), ["user"] = ("User", "المستخدم"), ["action"] = ("Action", "الإجراء"), ["entity"] = ("Item", "العنصر"), ["ip"] = ("IP address", "عنوان IP"),
        ["arrival"] = ("Arrival time", "وقت الوصول"), ["signIn"] = ("Parent signature (in)", "توقيع ولي الأمر (دخول)"), ["departure"] = ("Departure time", "وقت المغادرة"), ["signOut"] = ("Parent signature (out)", "توقيع ولي الأمر (خروج)"),
        ["generated"] = ("Generated", "أُنشئ في"), ["month"] = ("Month", "الشهر"), ["period"] = ("Period", "الفترة"), ["date"] = ("Date", "التاريخ"), ["expected"] = ("Expected", "المتوقع"), ["noRoom"] = ("No room", "بدون غرفة"),
        ["totalRow"] = ("Total", "المجموع"), ["parentsUsing"] = ("Parents who signed in", "أولياء الأمور الذين سجلوا الدخول"), ["staffUsing"] = ("Staff who signed in", "الموظفون الذين سجلوا الدخول"),
        ["attRep"] = ("Monthly attendance", "الحضور الشهري"), ["billRep"] = ("Billing and revenue", "الفواتير والإيرادات"), ["enqRep"] = ("Enquiry conversion", "تحويل الاستفسارات"), ["adoptP"] = ("Parent app use", "استخدام الأهل للتطبيق"),
        ["adoptS"] = ("Staff app use", "استخدام الموظفين للتطبيق"), ["auditRep"] = ("Audit log", "سجل التدقيق"), ["sheetRep"] = ("Daily sign-in sheet", "كشف الحضور اليومي"), ["rowsShown"] = ("Rows", "الصفوف"),
    };

    public static string T(string key, string lang) => Words.TryGetValue(key, out var w) ? (lang == "ar" ? w.Ar : w.En) : key;
}

/// <summary>Works out the numbers for each report. One method per report; each returns the same shape so every format can be drawn from it.</summary>
public sealed class ReportBuilder(IAppDbContext db, CentreCalendar calendar)
{
    private static string Money(long fils) => (fils / 100m).ToString("N2", CultureInfo.InvariantCulture);

    private static (DateOnly From, DateOnly To) Range(Dictionary<string, string?> p) =>
        (DateOnly.ParseExact(p["from"]!, "yyyy-MM-dd"), DateOnly.ParseExact(p["to"]!, "yyyy-MM-dd"));

    private static Guid? Id(Dictionary<string, string?> p, string key) => p.GetValueOrDefault(key) is { } v && Guid.TryParse(v, out var g) ? g : null;

    public async Task<ReportTable> BuildAsync(string kind, Dictionary<string, string?> p, CancellationToken ct)
    {
        var lang = p.GetValueOrDefault("language") ?? "en";
        string T(string key) => ReportText.T(key, lang);
        var generated = ($"{T("generated")}: {calendar.LocalNow:yyyy-MM-dd HH:mm}");

        switch (kind)
        {
            case "attendance_students":
            {
                var month = p["month"]!;
                var (from, to) = (DateOnly.ParseExact(month + "-01", "yyyy-MM-dd"), DateOnly.ParseExact(month + "-01", "yyyy-MM-dd").AddMonths(1).AddDays(-1));
                var kids = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
                if (Id(p, "room_id") is { } room) kids = kids.Where(s => s.RoomId == room);
                if (Id(p, "program_id") is { } program) kids = kids.Where(s => s.ProgramId == program);
                var list = await kids.OrderBy(s => s.FirstName).ThenBy(s => s.LastName).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.RoomId }).ToListAsync(ct);
                var ids = list.Select(k => k.Id).ToList();
                var counts = await db.AttendanceRecords.AsNoTracking().Where(a => a.PersonType == PersonTypes.Student && ids.Contains(a.PersonId) && a.Date >= from && a.Date <= to)
                    .GroupBy(a => new { a.PersonId, a.Status }).Select(g => new { g.Key.PersonId, g.Key.Status, N = g.Count() }).ToListAsync(ct);
                var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
                var rows = list.Select(k =>
                {
                    int Of(string s) => counts.Where(c => c.PersonId == k.Id && c.Status == s).Sum(c => c.N);
                    var all = counts.Where(c => c.PersonId == k.Id).Sum(c => c.N);
                    var attended = Of(AttendanceStatuses.Present) + Of(AttendanceStatuses.Late) + Of(AttendanceStatuses.EarlyLeave);
                    var roomName = k.RoomId is { } r && rooms.TryGetValue(r, out var n) ? (n.GetValueOrDefault(lang) ?? n.GetValueOrDefault("en")) : T("noRoom");
                    return (IReadOnlyList<object?>)[k.Name, roomName, Of(AttendanceStatuses.Present), Of(AttendanceStatuses.Late), Of(AttendanceStatuses.EarlyLeave), Of(AttendanceStatuses.Absent), all, all == 0 ? 0m : Math.Round(attended * 100m / all, 1)];
                }).ToList();
                return new ReportTable($"{T("attRep")} - {T("child")}", $"{T("month")}: {month}", lang, [T("child"), T("room"), T("present"), T("late"), T("early"), T("absent"), T("days"), T("rate")], rows,
                    [(T("children"), rows.Count.ToString(CultureInfo.InvariantCulture)), (T("generated"), calendar.LocalNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))]);
            }

            case "attendance_staff":
            {
                var month = p["month"]!;
                var (from, to) = (DateOnly.ParseExact(month + "-01", "yyyy-MM-dd"), DateOnly.ParseExact(month + "-01", "yyyy-MM-dd").AddMonths(1).AddDays(-1));
                var list = await db.Staff.AsNoTracking().OrderBy(s => s.FirstName).ThenBy(s => s.LastName).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.JobTitle }).ToListAsync(ct);
                var q = db.AttendanceRecords.AsNoTracking().Where(a => a.PersonType == PersonTypes.Staff && a.Date >= from && a.Date <= to);
                if (Id(p, "room_id") is { } room) q = q.Where(a => a.RoomId == room);
                var counts = await q.GroupBy(a => new { a.PersonId, a.Status }).Select(g => new { g.Key.PersonId, g.Key.Status, N = g.Count() }).ToListAsync(ct);
                var rows = list.Where(s => Id(p, "room_id") is null || counts.Any(c => c.PersonId == s.Id)).Select(s =>
                {
                    int Of(string st) => counts.Where(c => c.PersonId == s.Id && c.Status == st).Sum(c => c.N);
                    return (IReadOnlyList<object?>)[s.Name, s.JobTitle ?? string.Empty, Of(AttendanceStatuses.Present), Of(AttendanceStatuses.Late), Of(AttendanceStatuses.Absent), counts.Where(c => c.PersonId == s.Id).Sum(c => c.N)];
                }).ToList();
                return new ReportTable($"{T("attRep")} - {T("staff")}", $"{T("month")}: {month}", lang, [T("staff"), T("job"), T("present"), T("late"), T("absent"), T("days")], rows, [(T("staff"), rows.Count.ToString(CultureInfo.InvariantCulture))]);
            }

            case "billing_revenue":
            {
                var (from, to) = Range(p);
                var invoices = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled && i.IssueDate >= from && i.IssueDate <= to);
                if (p.GetValueOrDefault("status") is { Length: > 0 } status) invoices = invoices.Where(i => i.Status == status);
                var list = await invoices.OrderBy(i => i.IssueDate).ThenBy(i => i.InvoiceNumber)
                    .Select(i => new { i.InvoiceNumber, Child = db.Students.Where(s => s.Id == i.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(), i.IssueDate, i.DueDate, i.TotalFils, i.PaidFils, i.Status }).ToListAsync(ct);
                var rows = list.Select(i => (IReadOnlyList<object?>)[i.InvoiceNumber, i.Child ?? string.Empty, i.IssueDate.ToString("yyyy-MM-dd"), i.DueDate.ToString("yyyy-MM-dd"), i.TotalFils / 100m, i.PaidFils / 100m, (i.TotalFils - i.PaidFils) / 100m, i.Status]).ToList();
                var billed = list.Sum(i => i.TotalFils);
                var paid = list.Sum(i => i.PaidFils);
                return new ReportTable(T("billRep"), $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}", lang, [T("invoice"), T("child"), T("issued"), T("due"), T("total"), T("paid"), T("balance"), T("status")], rows,
                    [(T("billed"), Money(billed)), (T("collected"), Money(paid)), (T("outstanding"), Money(billed - paid)), (T("rowsShown"), rows.Count.ToString(CultureInfo.InvariantCulture))]);
            }

            case "enquiry_conversion":
            {
                var (from, to) = Range(p);
                var q = db.Enquiries.AsNoTracking().Where(e => e.EnquiryDate >= from && e.EnquiryDate <= to);
                if (p.GetValueOrDefault("source") is { Length: > 0 } source) q = q.Where(e => e.Source == source);
                if (Id(p, "staff_id") is { } staff) q = q.Where(e => e.AssignedStaffId == staff);
                var list = await q.Select(e => new { e.Source, e.Stage }).ToListAsync(ct);
                var rows = list.GroupBy(e => e.Source).OrderByDescending(g => g.Count()).Select(g =>
                {
                    var total = g.Count();
                    var toured = g.Count(e => e.Stage is EnquiryStages.Toured or EnquiryStages.Enrolled);
                    var enrolled = g.Count(e => e.Stage == EnquiryStages.Enrolled);
                    return (IReadOnlyList<object?>)[g.Key, total, toured, enrolled, Math.Round(enrolled * 100m / total, 1)];
                }).ToList();
                var all = list.Count;
                var won = list.Count(e => e.Stage == EnquiryStages.Enrolled);
                return new ReportTable(T("enqRep"), $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}", lang, [T("source"), T("enquiries"), T("toured"), T("enrolled"), T("conversion")], rows,
                    [(T("enquiries"), all.ToString(CultureInfo.InvariantCulture)), (T("enrolled"), won.ToString(CultureInfo.InvariantCulture)), (T("conversion"), (all == 0 ? 0m : Math.Round(won * 100m / all, 1)).ToString(CultureInfo.InvariantCulture))]);
            }

            case "adoption_parents":
            {
                var (from, to) = Range(p);
                var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var parents = await db.AppUserRoles.AsNoTracking().Where(r => r.Role!.Name == SystemRoles.Parent)
                    .Select(r => new { r.AppUser!.Id, Name = r.AppUser.FirstName + " " + r.AppUser.LastName, r.AppUser.Email, r.AppUser.LastLoginAt }).ToListAsync(ct);
                var kids = await db.StudentGuardians.AsNoTracking().Where(sg => sg.Guardian!.AppUserId != null)
                    .Select(sg => new { UserId = sg.Guardian!.AppUserId!.Value, Child = sg.Student!.FirstName + " " + sg.Student.LastName }).ToListAsync(ct);
                var rows = parents.OrderBy(x => x.Name).Select(x =>
                {
                    var used = x.LastLoginAt >= start && x.LastLoginAt < end;
                    return (IReadOnlyList<object?>)[$"{x.Name} ({x.Email})", string.Join(", ", kids.Where(k => k.UserId == x.Id).Select(k => k.Child)), x.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, used ? T("yes") : T("no")];
                }).ToList();
                var using_ = rows.Count(r => (string?)r[3] == T("yes"));
                return new ReportTable(T("adoptP"), $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}", lang, [T("parent"), T("children"), T("lastLogin"), T("usedApp")], rows,
                    [(T("parentsUsing"), $"{using_}/{rows.Count}"), (T("conversion"), (rows.Count == 0 ? 0m : Math.Round(using_ * 100m / rows.Count, 1)).ToString(CultureInfo.InvariantCulture) + "%")]);
            }

            case "adoption_staff":
            {
                var (from, to) = Range(p);
                var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var staff = await db.AppUserRoles.AsNoTracking().Where(r => r.Role!.Name != SystemRoles.Parent)
                    .Select(r => new { r.AppUser!.Id, Name = r.AppUser.FirstName + " " + r.AppUser.LastName, r.AppUser.Email, r.AppUser.LastLoginAt, Role = r.Role!.Name }).ToListAsync(ct);
                var rows = staff.GroupBy(s => s.Id).Select(g => g.First()).OrderBy(x => x.Name).Select(x =>
                {
                    var used = x.LastLoginAt >= start && x.LastLoginAt < end;
                    return (IReadOnlyList<object?>)[$"{x.Name} ({x.Email})", x.Role, x.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, used ? T("yes") : T("no")];
                }).ToList();
                var using_ = rows.Count(r => (string?)r[3] == T("yes"));
                return new ReportTable(T("adoptS"), $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}", lang, [T("staff"), T("job"), T("lastLogin"), T("usedApp")], rows,
                    [(T("staffUsing"), $"{using_}/{rows.Count}")]);
            }

            case "audit_log":
            {
                var (from, to) = Range(p);
                var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var q = db.AuditLogs.AsNoTracking().Where(a => a.OccurredAt >= start && a.OccurredAt < end);
                if (p.GetValueOrDefault("user_email") is { Length: > 0 } email) q = q.Where(a => a.UserEmail == email);
                if (p.GetValueOrDefault("entity_type") is { Length: > 0 } entity) q = q.Where(a => a.EntityType == entity);
                if (p.GetValueOrDefault("action") is { Length: > 0 } action) q = q.Where(a => a.Action == action);
                var list = await q.OrderByDescending(a => a.OccurredAt).Take(50_000).Select(a => new { a.OccurredAt, a.UserEmail, a.Action, a.EntityType, a.EntityId, a.IpAddress }).ToListAsync(ct);
                var rows = list.Select(a => (IReadOnlyList<object?>)[a.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"), a.UserEmail ?? string.Empty, a.Action, a.EntityType, a.EntityId.ToString(), a.IpAddress ?? string.Empty]).ToList();
                return new ReportTable(T("auditRep"), $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}", lang, [T("when"), T("user"), T("action"), T("entity"), "ID", T("ip")], rows, [(T("rowsShown"), rows.Count.ToString(CultureInfo.InvariantCulture))]);
            }

            case "sign_in_sheet":
            {
                var date = p.GetValueOrDefault("date") is { Length: > 0 } d ? DateOnly.ParseExact(d, "yyyy-MM-dd") : calendar.Today;
                var kids = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
                if (Id(p, "room_id") is { } room) kids = kids.Where(s => s.RoomId == room);
                var list = await kids.OrderBy(s => s.FirstName).ThenBy(s => s.LastName).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.RoomId }).ToListAsync(ct);
                var ids = list.Select(k => k.Id).ToList();
                var records = await db.AttendanceRecords.AsNoTracking().Where(a => a.PersonType == PersonTypes.Student && a.Date == date && ids.Contains(a.PersonId)).ToListAsync(ct);
                var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
                string Time(DateTimeOffset? t) => t is null ? string.Empty : TimeZoneInfo.ConvertTime(t.Value, calendar.Zone).ToString("HH:mm", CultureInfo.InvariantCulture);
                var rows = list.Select(k =>
                {
                    var rec = records.FirstOrDefault(r => r.PersonId == k.Id);
                    var roomName = k.RoomId is { } r && rooms.TryGetValue(r, out var n) ? (n.GetValueOrDefault(lang) ?? n.GetValueOrDefault("en")) : T("noRoom");
                    return (IReadOnlyList<object?>)[k.Name, roomName, Time(rec?.CheckInAt), string.Empty, Time(rec?.CheckOutAt), string.Empty];
                }).ToList();
                return new ReportTable(T("sheetRep"), $"{T("date")}: {date:yyyy-MM-dd}", lang, [T("child"), T("room"), T("arrival"), T("signIn"), T("departure"), T("signOut")], rows, [(T("children"), rows.Count.ToString(CultureInfo.InvariantCulture))]);
            }

            default:
                throw new InvalidOperationException($"Unknown report {kind}.");
        }
    }
}

/// <summary>Makes one requested report, stores the file for 30 days and tells the person who asked.</summary>
public sealed class ProcessReportRunHandler(IAppDbContext db, ReportBuilder builder, IReportRenderer renderer, Notifier notifier, IClock clock) : IRequestHandler<ProcessReportRunCommand, Result>
{
    public async Task<Result> Handle(ProcessReportRunCommand request, CancellationToken ct)
    {
        var run = await db.ReportRuns.FirstOrDefaultAsync(r => r.Id == request.RunId && r.Status == ReportStatuses.Pending, ct);
        if (run is null)
        {
            return Result.Success();
        }

        run.Status = ReportStatuses.Running;
        await db.SaveChangesAsync(ct);
        var lang = "en";
        try
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, string?>>(run.ParametersJson) ?? [];
            lang = parameters.GetValueOrDefault("language") ?? "en";
            var table = await builder.BuildAsync(run.Kind, parameters, ct);
            var file = renderer.Render(table, run.Format);
            run.Content = file.Content;
            run.ContentType = file.ContentType;
            run.FileName = $"{run.Kind}-{clock.UtcNow:yyyyMMdd-HHmm}.{file.Extension}";
            run.Status = ReportStatuses.Done;
            run.CompletedAt = clock.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = ReportStatuses.Failed;
            run.Error = "The report could not be made. Try again, or contact support if it keeps happening.";
            run.CompletedAt = clock.UtcNow;
            _ = ex;
        }

        await db.SaveChangesAsync(ct);
        var name = ReportCatalogue.All.First(r => r.Kind == run.Kind).Name;
        var ok = run.Status == ReportStatuses.Done;
        await notifier.NotifyAsync([run.RequestedBy], NotificationTiers.Operational, ok ? "report_ready" : "report_failed",
            new() { ["en"] = ok ? $"Report ready: {name["en"]}" : $"Report failed: {name["en"]}", ["ar"] = ok ? $"التقرير جاهز: {name["ar"]}" : $"فشل التقرير: {name["ar"]}" },
            new() { ["en"] = ok ? "Open Reports to download it. It is kept for 30 days." : "Open Reports to try again.", ["ar"] = ok ? "افتح التقارير لتنزيله. يُحفظ لمدة 30 يوماً." : "افتح التقارير للمحاولة مرة أخرى." }, "/reports", ct);
        _ = lang;
        return Result.Success();
    }
}

/// <summary>Picks up reports that were requested but never started (for example after a restart) and removes files older than 30 days.</summary>
public sealed class RunReportMaintenanceHandler(IAppDbContext db, ISender sender, IClock clock) : IRequestHandler<RunReportMaintenanceCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunReportMaintenanceCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var stuck = await db.ReportRuns.AsNoTracking().Where(r => r.Status == ReportStatuses.Pending && r.CreatedAt < now.AddMinutes(-2)).Select(r => r.Id).ToListAsync(ct);
        foreach (var id in stuck)
        {
            await sender.Send(new ProcessReportRunCommand(id), ct);
        }

        var expired = await db.ReportRuns.Where(r => r.ExpiresAt < now && r.Content != null).ToListAsync(ct);
        foreach (var run in expired)
        {
            run.Content = null;
            run.IsDeleted = true;
            run.DeletedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(stuck.Count + expired.Count);
    }
}
