using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Reporting;
using NMS.Application.Security;
using NMS.Domain.CentreManagement;
using NMS.Domain.Learning;
using NMS.Shared.Results;

namespace NMS.Application.Learning;

public static class LearningReportKinds
{
    public const string Progress = "progress";
    public const string Snapshot = "snapshot";
    public const string Comparison = "comparison";
    public const string ClassMilestone = "class_milestone";
    public const string StudentMilestone = "student_milestone";
    public const string Journey = "journey";

    public static readonly string[] All = [Progress, Snapshot, Comparison, ClassMilestone, StudentMilestone, Journey];

    /// <summary>Reports a family may also get, in their plain-language version.</summary>
    public static readonly string[] ForFamilies = [Progress, Snapshot, StudentMilestone, Journey];
}

/// <summary>
/// The five learning reports plus the learning journey, as PDF (and Excel/JSON for staff). Staff variant: full detail (codes, scores, notes).
/// Parent variant: levels and kind words only, from approved reports and shared moments, never scores, codes, notes or concerns.
/// </summary>
public sealed record LearningReportQuery(
    string Kind, Guid? StudentId, Guid? RunId, Guid? RoomId, Guid? TemplateId, string? TermA, string? TermB, string Variant, string Language, string Format) : IRequest<Result<RenderedReport>>;

public sealed class LearningReportValidator : AbstractValidator<LearningReportQuery>
{
    public LearningReportValidator()
    {
        RuleFor(q => q.Kind).Must(k => LearningReportKinds.All.Contains(k)).WithMessage("Unknown report.");
        RuleFor(q => q.Variant).Must(v => v is "staff" or "parent").WithMessage("Variant must be staff or parent.");
        RuleFor(q => q.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
        RuleFor(q => q.Format).Must(f => f is "pdf" or "excel" or "json").WithMessage("Format must be pdf, excel or json.");
    }
}

public sealed class LearningReportHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, IReportRenderer renderer)
    : IRequestHandler<LearningReportQuery, Result<RenderedReport>>
{
    private static readonly Dictionary<string, (string En, string Ar)> Labels = new()
    {
        ["child"] = ("Child", "الطفل"), ["checklist"] = ("Checklist", "قائمة التقييم"), ["term"] = ("Term", "الفصل"), ["date"] = ("Date", "التاريخ"), ["overall"] = ("Overall", "الإجمالي"),
        ["area"] = ("Area", "المجال"), ["code"] = ("Code", "الرمز"), ["milestone"] = ("Milestone", "المعلم"), ["score"] = ("Score", "التقدير"), ["note"] = ("Note", "ملاحظة"),
        ["level"] = ("Level", "المستوى"), ["summary"] = ("Summary", "الملخص"), ["staffNotes"] = ("Staff notes", "ملاحظات الموظفين"), ["class"] = ("Class", "الصف"),
        ["a"] = ("Term A", "الفصل أ"), ["b"] = ("Term B", "الفصل ب"), ["change"] = ("Change", "التغيير"), ["average"] = ("Average", "المتوسط"), ["assessed"] = ("Children assessed", "الأطفال المقيَّمون"),
        ["secure"] = ("Secure %", "متمكّن %"), ["emerging"] = ("Emerging or secure %", "في طور الظهور أو متمكّن %"), ["status"] = ("Status", "الحالة"), ["last"] = ("Last assessed", "آخر تقييم"),
        ["kind"] = ("Kind", "النوع"), ["what"] = ("What happened", "ما حدث"), ["moment"] = ("Moment", "لحظة"), ["report"] = ("Report", "تقرير"), ["concern"] = ("Concern", "قلق"),
        ["notYet"] = ("Not yet", "لم يتحقق بعد"), ["emergingW"] = ("Emerging", "في طور الظهور"), ["secureW"] = ("Secure", "متمكّن"), ["achieved"] = ("Achieved", "تحقق"),
        ["lvSecure"] = ("Going strong", "متمكّن"), ["lvGrowing"] = ("Growing well", "ينمو بشكل جيد"), ["lvStarting"] = ("Just starting", "في البداية"),
        ["tProgress"] = ("Assessment progress report", "تقرير تقدم التقييم"), ["tSnapshot"] = ("Assessment report", "تقرير التقييم"), ["tComparison"] = ("Comparison report", "تقرير المقارنة"),
        ["tClass"] = ("Class milestone report", "تقرير معالم الصف"), ["tStudent"] = ("Student milestone report", "تقرير معالم الطفل"), ["tJourney"] = ("Learning journey", "رحلة التعلّم"),
        ["children"] = ("Children", "الأطفال"), ["improved"] = ("Improved", "تحسّن"), ["runs"] = ("Assessments", "التقييمات"),
    };

    public async Task<Result<RenderedReport>> Handle(LearningReportQuery q, CancellationToken ct)
    {
        var staff = q.Variant == "staff";
        if (staff && !current.HasPermission(Permissions.LearningRead))
        {
            return Error.Forbidden("The staff version is for staff only.");
        }

        if (!staff && !LearningReportKinds.ForFamilies.Contains(q.Kind))
        {
            return Error.Validation("This report has no family version.", "kind");
        }

        if (!staff && q.Format != "pdf" && !current.HasPermission(Permissions.LearningRead))
        {
            return Error.Validation("The family version is a PDF.", "format");
        }

        var lang = q.Language;
        string L(string key) => lang == "ar" ? Labels[key].Ar : Labels[key].En;
        string T(Dictionary<string, string>? text) => text is null ? string.Empty : text.GetValueOrDefault(lang) ?? text.GetValueOrDefault("en") ?? text.Values.FirstOrDefault() ?? string.Empty;

        ReportTable? table = q.Kind switch
        {
            LearningReportKinds.Progress => await ProgressAsync(q, staff, L, T, ct),
            LearningReportKinds.Snapshot => await SnapshotAsync(q, staff, L, T, ct),
            LearningReportKinds.Comparison => await ComparisonAsync(q, L, T, ct),
            LearningReportKinds.ClassMilestone => await ClassAsync(q, L, T, ct),
            LearningReportKinds.StudentMilestone => await StudentMilestoneAsync(q, staff, L, T, ct),
            _ => await JourneyAsync(q, L, T, ct),
        };
        if (table is null)
        {
            return Error.NotFound("Nothing to report on. Check the child, class, checklist and terms you chose.");
        }

        return Result<RenderedReport>.Success(renderer.Render(table, q.Format));
    }

    private static string Level(int percent, Func<string, string> L) => percent >= 75 ? L("lvSecure") : percent >= 40 ? L("lvGrowing") : L("lvStarting");

    private static string Status(int score, int max, Func<string, string> L) => max == 1 ? (score >= 1 ? L("achieved") : L("notYet")) : score >= 2 ? L("secureW") : score == 1 ? L("emergingW") : L("notYet");

    private async Task<Dictionary<Guid, Dictionary<string, string>>> AreaNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.DevelopmentAreas.AsNoTracking().Where(a => list.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
    }

    private async Task<bool> CanSeeAsync(Guid studentId, CancellationToken ct) =>
        await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == studentId, ct);

    private async Task<List<AssessmentRunDto>> RunsAsync(Guid studentId, bool approvedOnly, CancellationToken ct)
    {
        var rows = db.AssessmentRuns.AsNoTracking().Where(r => r.StudentId == studentId);
        if (approvedOnly) rows = rows.Where(r => r.ParentApproved);
        var ids = await rows.OrderBy(r => r.AssessedOn).Select(r => r.Id).ToListAsync(ct);
        var result = new List<AssessmentRunDto>();
        foreach (var id in ids) result.Add((await RunMap.LoadAsync(db, id, ct))!);
        return result;
    }

    private async Task<ReportTable?> ProgressAsync(LearningReportQuery q, bool staff, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.StudentId is not { } sid || !await CanSeeAsync(sid, ct)) return null;
        var runs = await RunsAsync(sid, !staff, ct);
        if (runs.Count == 0) return null;
        var areaIds = runs.SelectMany(r => r.Scores.Select(s => s.AreaId)).Distinct().ToList();
        var names = await AreaNamesAsync(areaIds, ct);
        var columns = new List<string> { L("term"), L("date"), L("checklist") };
        columns.AddRange(areaIds.Select(a => T(names.GetValueOrDefault(a))));
        columns.Add(L("overall"));
        var rows = runs.Select(r =>
        {
            var max = RunMap.Max(r.ScoringKind);
            var row = new List<object?> { r.Term, r.AssessedOn.ToString("yyyy-MM-dd"), T(r.TemplateName) };
            foreach (var a in areaIds)
            {
                var inArea = r.Scores.Where(s => s.AreaId == a).ToList();
                var pct = inArea.Count == 0 ? 0 : (int)Math.Round(inArea.Sum(s => s.Score) * 100.0 / (inArea.Count * max));
                row.Add(inArea.Count == 0 ? string.Empty : staff ? $"{pct}%" : Level(pct, L));
            }

            row.Add(staff ? $"{r.ProgressPercent}%" : Level(r.ProgressPercent, L));
            return (IReadOnlyList<object?>)row;
        }).ToList();
        var name = runs[0].StudentName ?? string.Empty;
        return new ReportTable(L("tProgress"), name, q.Language, columns, rows, [(L("child"), name), (L("runs"), runs.Count.ToString())]);
    }

    private async Task<ReportTable?> SnapshotAsync(LearningReportQuery q, bool staff, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.RunId is not { } id) return null;
        var run = await RunMap.LoadAsync(db, id, ct);
        if (run is null || !await CanSeeAsync(run.StudentId, ct) || (!staff && !run.ParentApproved)) return null;
        var names = await AreaNamesAsync(run.Scores.Select(s => s.AreaId), ct);
        var max = RunMap.Max(run.ScoringKind);
        var summary = new List<(string, string)> { (L("child"), run.StudentName ?? string.Empty), (L("checklist"), T(run.TemplateName)), (L("term"), run.Term), (L("date"), run.AssessedOn.ToString("yyyy-MM-dd")) };
        if (staff)
        {
            summary.Add((L("overall"), $"{run.ProgressPercent}%"));
            if (run.StaffSummary is not null) summary.Add((L("staffNotes"), T(run.StaffSummary)));
            if (run.ParentSummary is not null) summary.Add((L("summary"), T(run.ParentSummary)));
            var rows = run.Scores.Select(s => (IReadOnlyList<object?>)new List<object?> { T(names.GetValueOrDefault(s.AreaId)), s.Code, T(s.Statement), Status(s.Score, max, L), s.Note ?? string.Empty }).ToList();
            return new ReportTable(L("tSnapshot"), run.StudentName, q.Language, [L("area"), L("code"), L("milestone"), L("score"), L("note")], rows, summary);
        }

        if (run.ParentSummary is not null) summary.Add((L("summary"), T(run.ParentSummary)));
        var levels = run.Scores.GroupBy(s => s.AreaId).Select(g =>
        {
            var pct = (int)Math.Round(g.Sum(s => s.Score) * 100.0 / (g.Count() * max));
            return (IReadOnlyList<object?>)new List<object?> { T(names.GetValueOrDefault(g.Key)), Level(pct, L) };
        }).ToList();
        return new ReportTable(L("tSnapshot"), run.StudentName, q.Language, [L("area"), L("level")], levels, summary);
    }

    private async Task<ReportTable?> ComparisonAsync(LearningReportQuery q, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.TemplateId is not { } tpl || string.IsNullOrWhiteSpace(q.TermA) || string.IsNullOrWhiteSpace(q.TermB)) return null;
        var visible = await StudentScope.VisibleAsync(db, scope, ct);
        if (q.RoomId is { } room) visible = visible.Where(s => s.RoomId == room);
        var students = await visible.Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var runs = await db.AssessmentRuns.AsNoTracking().Where(r => r.TemplateId == tpl && ids.Contains(r.StudentId) && (r.Term == q.TermA || r.Term == q.TermB)).ToListAsync(ct);
        var rows = new List<IReadOnlyList<object?>>();
        var a = new List<int>(); var b = new List<int>();
        foreach (var s in students)
        {
            var ra = runs.FirstOrDefault(r => r.StudentId == s.Id && r.Term == q.TermA);
            var rb = runs.FirstOrDefault(r => r.StudentId == s.Id && r.Term == q.TermB);
            if (ra is null || rb is null) continue;
            var pa = (await RunMap.LoadAsync(db, ra.Id, ct))!.ProgressPercent;
            var pb = (await RunMap.LoadAsync(db, rb.Id, ct))!.ProgressPercent;
            a.Add(pa); b.Add(pb);
            rows.Add(new List<object?> { s.Name, $"{pa}%", $"{pb}%", (pb - pa >= 0 ? "+" : string.Empty) + (pb - pa) });
        }

        if (rows.Count == 0) return null;
        var summary = new List<(string, string)> { (L("checklist"), (await db.AssessmentTemplates.AsNoTracking().Where(x => x.Id == tpl).Select(x => x.Name).FirstAsync(ct)) is { } n ? T(n) : string.Empty), (L("children"), rows.Count.ToString()),
            (L("average") + " " + q.TermA, $"{Math.Round(a.Average())}%"), (L("average") + " " + q.TermB, $"{Math.Round(b.Average())}%"), (L("improved"), a.Zip(b).Count(p => p.Second > p.First).ToString()) };
        return new ReportTable(L("tComparison"), $"{q.TermA} → {q.TermB}", q.Language, [L("child"), L("a") + " " + q.TermA, L("b") + " " + q.TermB, L("change")], rows, summary);
    }

    private async Task<ReportTable?> ClassAsync(LearningReportQuery q, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.RoomId is not { } room || q.TemplateId is not { } tpl) return null;
        var visible = (await StudentScope.VisibleAsync(db, scope, ct)).Where(s => s.RoomId == room).Select(s => s.Id);
        var ids = await visible.ToListAsync(ct);
        var latest = await db.AssessmentRuns.AsNoTracking().Where(r => r.TemplateId == tpl && ids.Contains(r.StudentId)).ToListAsync(ct);
        var perChild = latest.GroupBy(r => r.StudentId).Select(g => g.OrderByDescending(r => r.AssessedOn).First().Id).ToList();
        if (perChild.Count == 0) return null;
        var runs = new List<AssessmentRunDto>();
        foreach (var id in perChild) runs.Add((await RunMap.LoadAsync(db, id, ct))!);
        var max = RunMap.Max(runs[0].ScoringKind);
        var names = await AreaNamesAsync(runs.SelectMany(r => r.Scores.Select(s => s.AreaId)), ct);
        var rows = runs.SelectMany(r => r.Scores).GroupBy(s => s.MilestoneId).Select(g =>
        {
            var first = g.First();
            var n = g.Count();
            return (Area: T(names.GetValueOrDefault(first.AreaId)), first.Code, Text: T(first.Statement), N: n,
                Secure: (int)Math.Round(g.Count(s => s.Score >= max) * 100.0 / n), Emerging: (int)Math.Round(g.Count(s => s.Score >= 1) * 100.0 / n));
        }).OrderBy(x => x.Area).ThenBy(x => x.Code).Select(x => (IReadOnlyList<object?>)new List<object?> { x.Area, x.Code, x.Text, x.N, $"{x.Secure}%", $"{x.Emerging}%" }).ToList();
        var roomName = await db.Rooms.AsNoTracking().Where(x => x.Id == room).Select(x => x.Name).FirstAsync(ct);
        return new ReportTable(L("tClass"), T(roomName), q.Language, [L("area"), L("code"), L("milestone"), L("assessed"), L("secure"), L("emerging")], rows,
            [(L("class"), T(roomName)), (L("children"), runs.Count.ToString()), (L("checklist"), T(runs[0].TemplateName))]);
    }

    private async Task<ReportTable?> StudentMilestoneAsync(LearningReportQuery q, bool staff, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.StudentId is not { } sid || !await CanSeeAsync(sid, ct)) return null;
        var runs = await RunsAsync(sid, !staff, ct);
        if (runs.Count == 0) return null;
        var names = await AreaNamesAsync(runs.SelectMany(r => r.Scores.Select(s => s.AreaId)), ct);
        var latest = new Dictionary<Guid, (ScoreLineDto Line, AssessmentRunDto Run)>();
        foreach (var r in runs) foreach (var s in r.Scores) latest[s.MilestoneId] = (s, r);
        var name = runs[0].StudentName ?? string.Empty;
        var rows = latest.Values.Where(x => staff || x.Line.Score > 0).OrderBy(x => T(names.GetValueOrDefault(x.Line.AreaId))).ThenBy(x => x.Line.Code)
            .Select(x => (IReadOnlyList<object?>)(staff
                ? new List<object?> { T(names.GetValueOrDefault(x.Line.AreaId)), x.Line.Code, T(x.Line.Statement), Status(x.Line.Score, RunMap.Max(x.Run.ScoringKind), L), x.Run.AssessedOn.ToString("yyyy-MM-dd") }
                : new List<object?> { T(names.GetValueOrDefault(x.Line.AreaId)), T(x.Line.Statement), Status(x.Line.Score, RunMap.Max(x.Run.ScoringKind), L) })).ToList();
        return new ReportTable(L("tStudent"), name, q.Language, staff ? [L("area"), L("code"), L("milestone"), L("status"), L("last")] : [L("area"), L("milestone"), L("status")], rows, [(L("child"), name)]);
    }

    private async Task<ReportTable?> JourneyAsync(LearningReportQuery q, Func<string, string> L, Func<Dictionary<string, string>?, string> T, CancellationToken ct)
    {
        if (q.StudentId is not { } sid || !await CanSeeAsync(sid, ct)) return null;
        var staff = q.Variant == "staff";
        var obs = db.Observations.AsNoTracking().Where(o => o.StudentId == sid);
        if (!staff) obs = obs.Where(o => o.ShareWithParents && o.Type != ObservationTypes.Concern);
        var moments = await obs.OrderBy(o => o.ObservedAt).Take(300).ToListAsync(ct);
        var names = await AreaNamesAsync(moments.Where(o => o.AreaId != null).Select(o => o.AreaId!.Value), ct);
        var rows = moments.Select(o => (At: o.ObservedAt.DateTime, Row: (IReadOnlyList<object?>)new List<object?>
        {
            o.ObservedAt.ToString("yyyy-MM-dd"), o.Type == ObservationTypes.Concern ? L("concern") : L("moment"), o.AreaId is { } a ? T(names.GetValueOrDefault(a)) : string.Empty, T(o.Notes),
        })).ToList();
        foreach (var r in await RunsAsync(sid, !staff, ct))
        {
            rows.Add((r.AssessedOn.ToDateTime(TimeOnly.MinValue), new List<object?> { r.AssessedOn.ToString("yyyy-MM-dd"), L("report"), string.Empty, $"{T(r.TemplateName)} · {r.Term}. {T(staff ? r.StaffSummary ?? r.ParentSummary : r.ParentSummary)}".Trim() }));
        }

        if (rows.Count == 0) return null;
        var name = await StudentScope.NameAsync(db, sid, ct) ?? string.Empty;
        return new ReportTable(L("tJourney"), name, q.Language, [L("date"), L("kind"), L("area"), L("what")], rows.OrderBy(x => x.At).Select(x => x.Row).ToList(), [(L("child"), name)]);
    }
}
