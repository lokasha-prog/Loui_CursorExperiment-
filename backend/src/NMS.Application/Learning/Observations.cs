using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Application.Security;
using NMS.Domain.CentreManagement;
using NMS.Domain.Learning;
using NMS.Domain.Notifications;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Learning;

using Text = Dictionary<string, string>;

public sealed record ObservationDto(
    Guid Id, Guid StudentId, string? StudentName, string? ObserverName, DateTimeOffset ObservedAt, string Type, Guid? FrameworkId, Guid? AreaId, Guid? AgeBandId,
    IReadOnlyList<Guid> MilestoneIds, Text Notes, IReadOnlyList<Guid> MediaIds, bool ShareWithParents, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListObservationsQuery(Guid? StudentId, string? Type, Guid? AreaId, DateOnly? From, DateOnly? To, string? Cursor, int? Limit) : IRequest<Result<CursorPage<ObservationDto>>>;

public sealed record SaveObservationCommand(
    Guid? Id, Guid StudentId, DateTimeOffset? ObservedAt, string Type, Guid? FrameworkId, Guid? AreaId, Guid? AgeBandId, IReadOnlyList<Guid>? MilestoneIds,
    Text Notes, IReadOnlyList<Guid>? MediaIds, bool ShareWithParents) : IRequest<Result<ObservationDto>>;

public sealed record DeleteObservationCommand(Guid Id) : IRequest<Result>;

public sealed record AssessmentTemplateDto(Guid Id, Text Name, Guid FrameworkId, Guid? AgeBandId, string ScoringKind, IReadOnlyList<Guid> MilestoneIds, int Runs);

public sealed record ListTemplatesQuery : IRequest<Result<IReadOnlyList<AssessmentTemplateDto>>>;

public sealed record SaveTemplateCommand(Guid? Id, Text Name, Guid FrameworkId, Guid? AgeBandId, string ScoringKind, IReadOnlyList<Guid>? MilestoneIds) : IRequest<Result<AssessmentTemplateDto>>;

public sealed record DeleteTemplateCommand(Guid Id) : IRequest<Result>;

public sealed record ScoreLineDto(Guid MilestoneId, string Code, Text Statement, Guid AreaId, int Score, string? Note);

public sealed record AssessmentRunDto(
    Guid Id, Guid TemplateId, Text TemplateName, string ScoringKind, Guid StudentId, string? StudentName, string Term, DateOnly AssessedOn, string Status, bool ParentApproved,
    DateTimeOffset? ApprovedAt, Text? StaffSummary, Text? ParentSummary, int ProgressPercent, IReadOnlyList<ScoreLineDto> Scores) : ICursorItem
{
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record ListRunsQuery(Guid? StudentId, Guid? TemplateId, string? Term, string? Cursor, int? Limit) : IRequest<Result<CursorPage<AssessmentRunDto>>>;

public sealed record GetRunQuery(Guid Id) : IRequest<Result<AssessmentRunDto>>;

public sealed record StartRunCommand(Guid TemplateId, Guid StudentId, string Term, DateOnly? AssessedOn) : IRequest<Result<AssessmentRunDto>>;

public sealed record ScoreInput(Guid MilestoneId, int Score, string? Note);

public sealed record SaveScoresCommand(Guid RunId, IReadOnlyList<ScoreInput> Scores, Text? StaffSummary, Text? ParentSummary) : IRequest<Result<AssessmentRunDto>>;

public sealed record CompleteRunCommand(Guid RunId) : IRequest<Result<AssessmentRunDto>>;

public sealed record ApproveRunCommand(Guid RunId) : IRequest<Result<AssessmentRunDto>>;

public sealed record DeleteRunCommand(Guid RunId) : IRequest<Result>;

public sealed record ParentAreaDto(Text Name, string Level, int Percent);

public sealed record ParentReportDto(Guid Id, Text TemplateName, string? StudentName, string Term, DateOnly AssessedOn, DateTimeOffset? ApprovedAt, Text? Summary, IReadOnlyList<ParentAreaDto> Areas);

public sealed record ParentReportsQuery(Guid StudentId) : IRequest<Result<IReadOnlyList<ParentReportDto>>>;

public sealed record JourneyItemDto(string Kind, Guid Id, DateTimeOffset At, string? Type, Guid? AreaId, Text? AreaName, Text? Title, Text? Text, IReadOnlyList<Guid> MediaIds, int? Percent);

public sealed record JourneyDto(Guid StudentId, string? StudentName, IReadOnlyList<JourneyItemDto> Items);

public sealed record JourneyQuery(Guid StudentId) : IRequest<Result<JourneyDto>>;

public sealed class SaveObservationValidator : AbstractValidator<SaveObservationCommand>
{
    public SaveObservationValidator()
    {
        RuleFor(c => c.Type).Must(t => ObservationTypes.All.Contains(t)).WithMessage("Type must be formal, informal or concern.");
        RuleFor(c => c.Notes).Must(LearningRules.HasText).WithMessage("Notes are required.");
        RuleFor(c => c.MilestoneIds).Must(m => m is null || m.Count <= 40).WithMessage("At most 40 milestones.");
        RuleFor(c => c.MediaIds).Must(m => m is null || m.Count <= 10).WithMessage("At most 10 attachments.");
    }
}

public sealed class SaveTemplateValidator : AbstractValidator<SaveTemplateCommand>
{
    public SaveTemplateValidator()
    {
        RuleFor(c => c.Name).Must(LearningRules.HasText).WithMessage("A name is required.");
        RuleFor(c => c.ScoringKind).Must(k => ScoringKinds.All.Contains(k)).WithMessage("Scoring must be binary or three_point.");
        RuleFor(c => c.MilestoneIds).Must(m => m is { Count: > 0 and <= 300 }).WithMessage("Choose between 1 and 300 milestones.");
    }
}

public sealed class StartRunValidator : AbstractValidator<StartRunCommand>
{
    public StartRunValidator() => RuleFor(c => c.Term).NotEmpty().MaximumLength(60);
}

public sealed class SaveScoresValidator : AbstractValidator<SaveScoresCommand>
{
    public SaveScoresValidator()
    {
        RuleFor(c => c.Scores).NotNull().Must(s => s.Count <= 300).WithMessage("Too many scores.");
        RuleForEach(c => c.Scores).ChildRules(s => s.RuleFor(x => x.Note).MaximumLength(500));
    }
}

internal static class StudentScope
{
    /// <summary>Children this person may see: a parent their own, staff the children of their rooms, managers everyone.</summary>
    public static async Task<IQueryable<Student>> VisibleAsync(IAppDbContext db, IAccessScope scope, CancellationToken ct)
    {
        var own = await scope.GetOwnStudentIdsAsync(ct);
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        var students = db.Students.AsNoTracking();
        if (own is not null)
        {
            students = students.Where(s => own.Contains(s.Id));
        }
        else if (rooms is not null)
        {
            students = students.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
        }

        return students;
    }

    public static async Task<string?> NameAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Students.AsNoTracking().Where(s => s.Id == id).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct);
}

internal static class ObservationMap
{
    public static ObservationDto ToDto(Observation o, string? student, string? observer) =>
        new(o.Id, o.StudentId, student, observer, o.ObservedAt, o.Type, o.FrameworkId, o.AreaId, o.AgeBandId, o.MilestoneIds.Select(Guid.Parse).ToList(), o.Notes,
            o.MediaIds.Select(Guid.Parse).ToList(), o.ShareWithParents, o.CreatedAt);

    public static async Task<Dictionary<Guid, string>> StudentNamesAsync(IAppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FirstName + " " + s.LastName, ct);

    public static async Task<Dictionary<Guid, string>> UserNamesAsync(IAppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
}

public sealed class ListObservationsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListObservationsQuery, Result<CursorPage<ObservationDto>>>
{
    public async Task<Result<CursorPage<ObservationDto>>> Handle(ListObservationsQuery request, CancellationToken ct)
    {
        var visible = (await StudentScope.VisibleAsync(db, scope, ct)).Select(s => s.Id);
        var rows = db.Observations.AsNoTracking().Where(o => visible.Contains(o.StudentId));
        if (request.StudentId is { } s) rows = rows.Where(o => o.StudentId == s);
        if (request.Type is { } t) rows = rows.Where(o => o.Type == t);
        if (request.AreaId is { } a) rows = rows.Where(o => o.AreaId == a);
        if (request.From is { } f) { var from = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); rows = rows.Where(o => o.ObservedAt >= from); }
        if (request.To is { } to) { var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); rows = rows.Where(o => o.ObservedAt < end); }

        var page = await rows.ToCursorPageAsync(request.Cursor, request.Limit, o => new Header(o.Id, o.CreatedAt), ct);
        var ids = page.Items.Select(h => h.Id).ToList();
        var full = (await db.Observations.AsNoTracking().Where(o => ids.Contains(o.Id)).ToListAsync(ct)).ToDictionary(o => o.Id);
        var students = await ObservationMap.StudentNamesAsync(db, full.Values.Select(o => o.StudentId).Distinct().ToList(), ct);
        var users = await ObservationMap.UserNamesAsync(db, full.Values.Select(o => o.ObserverUserId).Distinct().ToList(), ct);
        var items = ids.Select(id => ObservationMap.ToDto(full[id], students.GetValueOrDefault(full[id].StudentId), users.GetValueOrDefault(full[id].ObserverUserId))).ToList();
        return Result<CursorPage<ObservationDto>>.Success(new CursorPage<ObservationDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }

    private sealed record Header(Guid Id, DateTimeOffset CreatedAt) : ICursorItem;
}

public sealed class SaveObservationHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, IClock clock, Notifier notifier)
    : IRequestHandler<SaveObservationCommand, Result<ObservationDto>>
{
    public async Task<Result<ObservationDto>> Handle(SaveObservationCommand request, CancellationToken ct)
    {
        if (!await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == request.StudentId, ct))
        {
            return Error.Validation("The child was not found.", "student_id");
        }

        var milestones = (request.MilestoneIds ?? []).Distinct().ToList();
        if (request.FrameworkId is null && (request.AreaId is not null || request.AgeBandId is not null || milestones.Count > 0))
        {
            return Error.Validation("Choose the curriculum first.", "framework_id");
        }

        if (request.FrameworkId is { } fw)
        {
            if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == fw, ct)) return Error.Validation("The curriculum was not found.", "framework_id");
            if (request.AreaId is { } area && !await db.DevelopmentAreas.AnyAsync(a => a.Id == area && a.FrameworkId == fw, ct)) return Error.Validation("The area is not part of this curriculum.", "area_id");
            if (request.AgeBandId is { } band && !await db.AgeBands.AnyAsync(a => a.Id == band && a.FrameworkId == fw, ct)) return Error.Validation("The age band is not part of this curriculum.", "age_band_id");
            if (milestones.Count > 0 && await db.Milestones.CountAsync(m => m.FrameworkId == fw && milestones.Contains(m.Id), ct) != milestones.Count) return Error.Validation("A milestone is not part of this curriculum.", "milestone_ids");
        }

        var media = (request.MediaIds ?? []).Distinct().ToList();
        if (media.Count > 0 && await db.MediaFiles.CountAsync(m => media.Contains(m.Id), ct) != media.Count)
        {
            return Error.Validation("An attachment was not found.", "media_ids");
        }

        Observation row;
        var isNew = request.Id is null;
        if (request.Id is { } id)
        {
            var found = await db.Observations.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (found is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == found.StudentId, ct)) return Error.NotFound("Observation not found.");
            if (found.ObserverUserId != current.UserId && !current.HasPermission(Permissions.LearningApprove)) return Error.Forbidden("Only the person who wrote it or a manager can change it.");
            row = found;
        }
        else
        {
            row = new Observation { ObserverUserId = current.UserId };
            db.Observations.Add(row);
        }

        row.StudentId = request.StudentId;
        row.ObservedAt = request.ObservedAt ?? clock.UtcNow;
        row.Type = request.Type;
        row.FrameworkId = request.FrameworkId;
        row.AreaId = request.AreaId;
        row.AgeBandId = request.AgeBandId;
        row.MilestoneIds = milestones.Select(m => m.ToString()).ToList();
        row.Notes = request.Notes;
        row.MediaIds = media.Select(m => m.ToString()).ToList();
        row.ShareWithParents = request.Type != ObservationTypes.Concern && request.ShareWithParents;
        await db.SaveChangesAsync(ct);

        if (isNew && row.Type == ObservationTypes.Concern)
        {
            var name = await StudentScope.NameAsync(db, row.StudentId, ct) ?? string.Empty;
            var managers = await notifier.UsersWithPermissionAsync(Permissions.LearningApprove, ct);
            await notifier.NotifyAsync(managers, NotificationTiers.Operational, "learning_concern",
                new() { ["en"] = "A learning concern was recorded", ["ar"] = "تم تسجيل ملاحظة قلق تعليمي" },
                new() { ["en"] = $"A concern was recorded for {name}. Please review it.", ["ar"] = $"تم تسجيل ملاحظة قلق بشأن {name}. يرجى مراجعتها." }, "/learning", ct);
        }

        var student = await StudentScope.NameAsync(db, row.StudentId, ct);
        var observer = (await ObservationMap.UserNamesAsync(db, [row.ObserverUserId], ct)).GetValueOrDefault(row.ObserverUserId);
        return Result<ObservationDto>.Success(ObservationMap.ToDto(row, student, observer));
    }
}

public sealed class DeleteObservationHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current) : IRequestHandler<DeleteObservationCommand, Result>
{
    public async Task<Result> Handle(DeleteObservationCommand request, CancellationToken ct)
    {
        var row = await db.Observations.FirstOrDefaultAsync(o => o.Id == request.Id, ct);
        if (row is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == row.StudentId, ct))
        {
            return Result.Failure(Error.NotFound("Observation not found."));
        }

        if (row.ObserverUserId != current.UserId && !current.HasPermission(Permissions.LearningApprove))
        {
            return Result.Failure(Error.Forbidden("Only the person who wrote it or a manager can delete it."));
        }

        db.Observations.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ListTemplatesHandler(IAppDbContext db) : IRequestHandler<ListTemplatesQuery, Result<IReadOnlyList<AssessmentTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<AssessmentTemplateDto>>> Handle(ListTemplatesQuery request, CancellationToken ct)
    {
        var rows = await db.AssessmentTemplates.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(ct);
        var runs = await db.AssessmentRuns.AsNoTracking().GroupBy(r => r.TemplateId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return Result<IReadOnlyList<AssessmentTemplateDto>>.Success(rows.Select(t => new AssessmentTemplateDto(t.Id, t.Name, t.FrameworkId, t.AgeBandId, t.ScoringKind, t.MilestoneIds.Select(Guid.Parse).ToList(), runs.GetValueOrDefault(t.Id))).ToList());
    }
}

public sealed class SaveTemplateHandler(IAppDbContext db) : IRequestHandler<SaveTemplateCommand, Result<AssessmentTemplateDto>>
{
    public async Task<Result<AssessmentTemplateDto>> Handle(SaveTemplateCommand request, CancellationToken ct)
    {
        if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == request.FrameworkId, ct)) return Error.Validation("The curriculum was not found.", "framework_id");
        if (request.AgeBandId is { } band && !await db.AgeBands.AnyAsync(a => a.Id == band && a.FrameworkId == request.FrameworkId, ct)) return Error.Validation("The age band is not part of this curriculum.", "age_band_id");
        var milestones = request.MilestoneIds!.Distinct().ToList();
        if (await db.Milestones.CountAsync(m => m.FrameworkId == request.FrameworkId && milestones.Contains(m.Id), ct) != milestones.Count) return Error.Validation("A milestone is not part of this curriculum.", "milestone_ids");

        AssessmentTemplate row;
        if (request.Id is { } id)
        {
            row = await db.AssessmentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new KeyNotFoundException();
            if (row.ScoringKind != request.ScoringKind && await db.AssessmentRuns.AnyAsync(r => r.TemplateId == id, ct)) return Error.InvalidState("The scoring cannot change once assessments exist.");
        }
        else
        {
            row = new AssessmentTemplate();
            db.AssessmentTemplates.Add(row);
        }

        row.Name = request.Name;
        row.FrameworkId = request.FrameworkId;
        row.AgeBandId = request.AgeBandId;
        row.ScoringKind = request.ScoringKind;
        row.MilestoneIds = milestones.Select(m => m.ToString()).ToList();
        await db.SaveChangesAsync(ct);
        return Result<AssessmentTemplateDto>.Success(new AssessmentTemplateDto(row.Id, row.Name, row.FrameworkId, row.AgeBandId, row.ScoringKind, milestones, await db.AssessmentRuns.CountAsync(r => r.TemplateId == row.Id, ct)));
    }
}

public sealed class DeleteTemplateHandler(IAppDbContext db) : IRequestHandler<DeleteTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteTemplateCommand request, CancellationToken ct)
    {
        var row = await db.AssessmentTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (row is null) return Result.Failure(Error.NotFound("Template not found."));
        if (await db.AssessmentRuns.AnyAsync(r => r.TemplateId == row.Id, ct)) return Result.Failure(Error.InvalidState("This template has assessments. It cannot be deleted."));
        db.AssessmentTemplates.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal static class RunMap
{
    public static int Max(string kind) => kind == ScoringKinds.Binary ? 1 : 2;

    public static async Task<AssessmentRunDto?> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return null;
        var template = await db.AssessmentTemplates.AsNoTracking().FirstAsync(t => t.Id == run.TemplateId, ct);
        var scores = await db.AssessmentScores.AsNoTracking().Where(s => s.RunId == id).ToListAsync(ct);
        var ids = scores.Select(s => s.MilestoneId).ToList();
        var milestones = (await db.Milestones.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct)).ToDictionary(m => m.Id);
        var lines = scores.Where(s => milestones.ContainsKey(s.MilestoneId)).Select(s => new { s, m = milestones[s.MilestoneId] }).OrderBy(x => x.m.AreaId).ThenBy(x => x.m.SortOrder)
            .Select(x => new ScoreLineDto(x.m.Id, x.m.Code, x.m.Statement, x.m.AreaId, x.s.Score, x.s.Note)).ToList();
        var max = Max(template.ScoringKind);
        var percent = lines.Count == 0 ? 0 : (int)Math.Round(lines.Sum(l => l.Score) * 100.0 / (lines.Count * max));
        var name = await StudentScope.NameAsync(db, run.StudentId, ct);
        return new AssessmentRunDto(run.Id, run.TemplateId, template.Name, template.ScoringKind, run.StudentId, name, run.Term, run.AssessedOn, run.Status, run.ParentApproved, run.ApprovedAt,
            run.StaffSummary, run.ParentSummary, percent, lines) { CreatedAt = run.CreatedAt };
    }
}

public sealed class ListRunsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListRunsQuery, Result<CursorPage<AssessmentRunDto>>>
{
    public async Task<Result<CursorPage<AssessmentRunDto>>> Handle(ListRunsQuery request, CancellationToken ct)
    {
        var visible = (await StudentScope.VisibleAsync(db, scope, ct)).Select(s => s.Id);
        var rows = db.AssessmentRuns.AsNoTracking().Where(r => visible.Contains(r.StudentId));
        if (request.StudentId is { } s) rows = rows.Where(r => r.StudentId == s);
        if (request.TemplateId is { } t) rows = rows.Where(r => r.TemplateId == t);
        if (!string.IsNullOrWhiteSpace(request.Term)) { var term = request.Term.Trim(); rows = rows.Where(r => r.Term == term); }
        var page = await rows.ToCursorPageAsync(request.Cursor, request.Limit, r => new Header(r.Id, r.CreatedAt), ct);
        var items = new List<AssessmentRunDto>();
        foreach (var h in page.Items)
        {
            items.Add((await RunMap.LoadAsync(db, h.Id, ct))!);
        }

        return Result<CursorPage<AssessmentRunDto>>.Success(new CursorPage<AssessmentRunDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }

    private sealed record Header(Guid Id, DateTimeOffset CreatedAt) : ICursorItem;
}

public sealed class GetRunHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetRunQuery, Result<AssessmentRunDto>>
{
    public async Task<Result<AssessmentRunDto>> Handle(GetRunQuery request, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (run is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == run.StudentId, ct)) return Error.NotFound("Assessment not found.");
        return Result<AssessmentRunDto>.Success((await RunMap.LoadAsync(db, run.Id, ct))!);
    }
}

public sealed class StartRunHandler(IAppDbContext db, IAccessScope scope, NMS.Application.Attendance.CentreCalendar calendar) : IRequestHandler<StartRunCommand, Result<AssessmentRunDto>>
{
    public async Task<Result<AssessmentRunDto>> Handle(StartRunCommand request, CancellationToken ct)
    {
        var template = await db.AssessmentTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (template is null) return Error.NotFound("Template not found.");
        if (!await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == request.StudentId, ct)) return Error.Validation("The child was not found.", "student_id");
        var term = request.Term.Trim();
        if (await db.AssessmentRuns.AnyAsync(r => r.TemplateId == template.Id && r.StudentId == request.StudentId && r.Term == term, ct)) return Error.Conflict("This child already has this assessment for this term.");

        var run = new AssessmentRun { TemplateId = template.Id, StudentId = request.StudentId, Term = term, AssessedOn = request.AssessedOn ?? calendar.Today };
        db.AssessmentRuns.Add(run);
        foreach (var m in template.MilestoneIds)
        {
            db.AssessmentScores.Add(new AssessmentScore { RunId = run.Id, MilestoneId = Guid.Parse(m), Score = 0 });
        }

        await db.SaveChangesAsync(ct);
        return Result<AssessmentRunDto>.Success((await RunMap.LoadAsync(db, run.Id, ct))!);
    }
}

public sealed class SaveScoresHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<SaveScoresCommand, Result<AssessmentRunDto>>
{
    public async Task<Result<AssessmentRunDto>> Handle(SaveScoresCommand request, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct);
        if (run is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == run.StudentId, ct)) return Error.NotFound("Assessment not found.");
        var template = await db.AssessmentTemplates.AsNoTracking().FirstAsync(t => t.Id == run.TemplateId, ct);
        var max = RunMap.Max(template.ScoringKind);
        var rows = await db.AssessmentScores.Where(s => s.RunId == run.Id).ToListAsync(ct);
        foreach (var input in request.Scores)
        {
            var row = rows.FirstOrDefault(r => r.MilestoneId == input.MilestoneId);
            if (row is null) return Error.Validation("A milestone is not part of this assessment.", "scores");
            if (input.Score < 0 || input.Score > max) return Error.Validation($"Scores go from 0 to {max}.", "scores");
            row.Score = input.Score;
            row.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        }

        if (request.StaffSummary is not null) run.StaffSummary = request.StaffSummary.Count == 0 ? null : request.StaffSummary;
        if (request.ParentSummary is not null) run.ParentSummary = request.ParentSummary.Count == 0 ? null : request.ParentSummary;

        // Any change after approval means the parent version must be approved again.
        run.ParentApproved = false;
        run.ApprovedAt = null;
        run.ApprovedBy = null;
        await db.SaveChangesAsync(ct);
        return Result<AssessmentRunDto>.Success((await RunMap.LoadAsync(db, run.Id, ct))!);
    }
}

public sealed class CompleteRunHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<CompleteRunCommand, Result<AssessmentRunDto>>
{
    public async Task<Result<AssessmentRunDto>> Handle(CompleteRunCommand request, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct);
        if (run is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == run.StudentId, ct)) return Error.NotFound("Assessment not found.");
        run.Status = AssessmentStatuses.Completed;
        await db.SaveChangesAsync(ct);
        return Result<AssessmentRunDto>.Success((await RunMap.LoadAsync(db, run.Id, ct))!);
    }
}

public sealed class ApproveRunHandler(IAppDbContext db, ICurrentUser current, IClock clock, Notifier notifier) : IRequestHandler<ApproveRunCommand, Result<AssessmentRunDto>>
{
    public async Task<Result<AssessmentRunDto>> Handle(ApproveRunCommand request, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct);
        if (run is null) return Error.NotFound("Assessment not found.");
        if (run.Status != AssessmentStatuses.Completed) return Error.InvalidState("Complete the assessment before approving the parent version.");
        if (run.ParentSummary is not { Count: > 0 }) return Error.InvalidState("Write the parent summary first.");

        run.ParentApproved = true;
        run.ApprovedBy = current.UserId;
        run.ApprovedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        var users = await db.StudentGuardians.AsNoTracking()
            .Where(sg => sg.StudentId == run.StudentId && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);
        await notifier.NotifyAsync(users, NotificationTiers.Informational, "learning_report",
            new() { ["en"] = "A new learning report is ready", ["ar"] = "تقرير تعلّم جديد جاهز" }, run.ParentSummary, "/learning", ct);
        return Result<AssessmentRunDto>.Success((await RunMap.LoadAsync(db, run.Id, ct))!);
    }
}

public sealed class DeleteRunHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<DeleteRunCommand, Result>
{
    public async Task<Result> Handle(DeleteRunCommand request, CancellationToken ct)
    {
        var run = await db.AssessmentRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct);
        if (run is null || !await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == run.StudentId, ct)) return Result.Failure(Error.NotFound("Assessment not found."));
        if (run.ParentApproved) return Result.Failure(Error.InvalidState("An approved assessment cannot be deleted."));
        db.AssessmentScores.RemoveRange(await db.AssessmentScores.Where(s => s.RunId == run.Id).ToListAsync(ct));
        db.AssessmentRuns.Remove(run);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>The plain-language version for families: levels per area, a summary, no codes, scores, notes or concerns.</summary>
public sealed class ParentReportsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ParentReportsQuery, Result<IReadOnlyList<ParentReportDto>>>
{
    public async Task<Result<IReadOnlyList<ParentReportDto>>> Handle(ParentReportsQuery request, CancellationToken ct)
    {
        if (!await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == request.StudentId, ct)) return Error.NotFound("Child not found.");
        var runs = await db.AssessmentRuns.AsNoTracking().Where(r => r.StudentId == request.StudentId && r.ParentApproved).OrderByDescending(r => r.AssessedOn).ToListAsync(ct);
        var result = new List<ParentReportDto>();
        foreach (var run in runs)
        {
            var full = (await RunMap.LoadAsync(db, run.Id, ct))!;
            var areas = await db.DevelopmentAreas.AsNoTracking().Where(a => full.Scores.Select(s => s.AreaId).Contains(a.Id)).OrderBy(a => a.SortOrder).ToListAsync(ct);
            var max = RunMap.Max(full.ScoringKind);
            var levels = areas.Select(a =>
            {
                var inArea = full.Scores.Where(s => s.AreaId == a.Id).ToList();
                var percent = (int)Math.Round(inArea.Sum(s => s.Score) * 100.0 / (inArea.Count * max));
                return new ParentAreaDto(a.Name, percent >= 75 ? "secure" : percent >= 40 ? "growing" : "starting", percent);
            }).ToList();
            result.Add(new ParentReportDto(run.Id, full.TemplateName, full.StudentName, run.Term, run.AssessedOn, run.ApprovedAt, run.ParentSummary, levels));
        }

        return Result<IReadOnlyList<ParentReportDto>>.Success(result);
    }
}

/// <summary>A child's story in time order. Staff see everything; a family sees only what was shared with them and approved reports.</summary>
public sealed class JourneyHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<JourneyQuery, Result<JourneyDto>>
{
    public async Task<Result<JourneyDto>> Handle(JourneyQuery request, CancellationToken ct)
    {
        if (!await (await StudentScope.VisibleAsync(db, scope, ct)).AnyAsync(s => s.Id == request.StudentId, ct)) return Error.NotFound("Child not found.");
        var family = await scope.GetOwnStudentIdsAsync(ct) is not null;
        var obs = db.Observations.AsNoTracking().Where(o => o.StudentId == request.StudentId);
        if (family) obs = obs.Where(o => o.ShareWithParents && o.Type != ObservationTypes.Concern);
        var observations = await obs.OrderByDescending(o => o.ObservedAt).Take(200).ToListAsync(ct);
        var areaIds = observations.Where(o => o.AreaId != null).Select(o => o.AreaId!.Value).Distinct().ToList();
        var areaNames = await db.DevelopmentAreas.AsNoTracking().Where(a => areaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var items = observations.Select(o => new JourneyItemDto("observation", o.Id, o.ObservedAt, family ? null : o.Type, o.AreaId, o.AreaId is { } a ? areaNames.GetValueOrDefault(a) : null,
            null, o.Notes, o.MediaIds.Select(Guid.Parse).ToList(), null)).ToList();

        var runs = db.AssessmentRuns.AsNoTracking().Where(r => r.StudentId == request.StudentId);
        if (family) runs = runs.Where(r => r.ParentApproved);
        foreach (var run in await runs.OrderByDescending(r => r.AssessedOn).Take(50).ToListAsync(ct))
        {
            var full = (await RunMap.LoadAsync(db, run.Id, ct))!;
            items.Add(new JourneyItemDto("assessment", run.Id, new DateTimeOffset(run.AssessedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), null, null, null, full.TemplateName,
                family ? run.ParentSummary : run.StaffSummary ?? run.ParentSummary, [], full.ProgressPercent));
        }

        var name = await StudentScope.NameAsync(db, request.StudentId, ct);
        return Result<JourneyDto>.Success(new JourneyDto(request.StudentId, name, items.OrderByDescending(i => i.At).ToList()));
    }
}
