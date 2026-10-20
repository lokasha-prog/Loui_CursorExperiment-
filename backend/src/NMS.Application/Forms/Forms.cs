using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Enquiries;
using NMS.Domain.Forms;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Forms;

public sealed record FormDto(
    Guid Id, Dictionary<string, string> Name, Dictionary<string, string>? Description, string Type, string Status, bool IsPublic, int PublishedVersion,
    int Submissions, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record FormDetailDto(FormDto Form, int EditingVersion, bool HasUnpublishedChanges, IReadOnlyList<FormField> Fields, IReadOnlyList<FormField> PublishedFields);

public sealed record FillableFormDto(Guid Id, Dictionary<string, string> Name, Dictionary<string, string>? Description, string Type, int Version, IReadOnlyList<FormField> Fields);

public sealed record SubmissionDto(Guid Id, int Version, Guid? StudentId, string? StudentName, Guid? EnquiryId, Dictionary<string, JsonElement> Answers, int? DurationSeconds, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record DropOff(string FieldId, int Count);

public sealed record FormAnalyticsDto(int Starts, int Submissions, double CompletionRate, double AverageSeconds, IReadOnlyList<DropOff> DropOffs);

public sealed record ListFormsQuery(string? Type, string? Status, string? Cursor, int? Limit) : IRequest<Result<CursorPage<FormDto>>>;

public sealed record GetFormQuery(Guid Id) : IRequest<Result<FormDetailDto>>;

public sealed record CreateFormCommand(Dictionary<string, string> Name, Dictionary<string, string>? Description, string Type, bool IsPublic) : IRequest<Result<FormDetailDto>>;

public sealed record UpdateFormCommand(Guid Id, Dictionary<string, string> Name, Dictionary<string, string>? Description, string Type, bool IsPublic) : IRequest<Result<FormDetailDto>>;

public sealed record SaveFieldsCommand(Guid Id, List<FormField> Fields) : IRequest<Result<FormDetailDto>>;

public sealed record PublishFormCommand(Guid Id) : IRequest<Result<FormDetailDto>>;

public sealed record SetFormArchivedCommand(Guid Id, bool Archived) : IRequest<Result<FormDetailDto>>;

public sealed record GetFillableFormQuery(Guid Id, string? Centre) : IRequest<Result<FillableFormDto>>;

public sealed record StartFormCommand(Guid Id, string? Centre) : IRequest<Result<Guid>>;

public sealed record FormProgressCommand(Guid Id, Guid StartId, string FieldId, string? Centre) : IRequest<Result>;

public sealed record SubmitFormCommand(
    Guid Id, Dictionary<string, JsonElement> Answers, Guid? StudentId, Guid? EnquiryId, Guid? StartId, string? Centre, string? CaptchaToken, string? IpAddress) : IRequest<Result<Guid>>;

public sealed record ListSubmissionsQuery(Guid Id, string? Cursor, int? Limit) : IRequest<Result<CursorPage<SubmissionDto>>>;

public sealed record FormAnalyticsQuery(Guid Id, DateOnly? From, DateOnly? To) : IRequest<Result<FormAnalyticsDto>>;

public sealed class CreateFormValidator : AbstractValidator<CreateFormCommand>
{
    public CreateFormValidator()
    {
        RuleFor(x => x.Name).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 150).WithMessage("Give the form an English name (up to 150 characters).");
        RuleFor(x => x.Type).Must(t => FormTypes.All.Contains(t)).WithMessage("Unknown form type.");
    }
}

public sealed class UpdateFormValidator : AbstractValidator<UpdateFormCommand>
{
    public UpdateFormValidator()
    {
        RuleFor(x => x.Name).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 150).WithMessage("Give the form an English name (up to 150 characters).");
        RuleFor(x => x.Type).Must(t => FormTypes.All.Contains(t)).WithMessage("Unknown form type.");
    }
}

/// <summary>What makes a form definition acceptable: known field types, unique ids, choices where needed, and rules that point at earlier questions.</summary>
public sealed class SaveFieldsValidator : AbstractValidator<SaveFieldsCommand>
{
    public SaveFieldsValidator()
    {
        RuleFor(x => x.Fields).Must(f => f is { Count: <= 100 }).WithMessage("A form can have at most 100 fields.");
        RuleFor(x => x.Fields).Custom((fields, context) =>
        {
            var seen = new HashSet<string>();
            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Id) || field.Id.Length > 60 || !field.Id.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
                {
                    context.AddFailure("fields", "Every field needs a short id of letters, digits, - or _.");
                    continue;
                }

                if (!seen.Add(field.Id))
                {
                    context.AddFailure("fields", $"The field id '{field.Id}' is used twice.");
                }

                if (!FieldTypes.All.Contains(field.Type))
                {
                    context.AddFailure("fields", $"Unknown field type '{field.Type}'.");
                }

                if (field.Label is null || !field.Label.TryGetValue("en", out var label) || string.IsNullOrWhiteSpace(label) || label.Length > 300)
                {
                    context.AddFailure("fields", $"Field '{field.Id}' needs an English label.");
                }

                if (FieldTypes.WithOptions.Contains(field.Type) && (field.Options is not { Count: > 0 } || field.Options.Any(o => string.IsNullOrWhiteSpace(o.Value)) || field.Options.Select(o => o.Value).Distinct().Count() != field.Options.Count))
                {
                    context.AddFailure("fields", $"Field '{field.Id}' needs its choices, each with a different value.");
                }

                if (field.ShowWhenFieldId is not null && (field.ShowWhenFieldId == field.Id || !fields.TakeWhile(f => f != field).Any(f => f.Id == field.ShowWhenFieldId)))
                {
                    context.AddFailure("fields", $"The rule on '{field.Id}' must point at a field above it.");
                }
            }
        });
    }
}

internal static class FormJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, DictionaryKeyPolicy = null };

    public static List<FormField> Fields(string json) => JsonSerializer.Deserialize<List<FormField>>(json, Options) ?? [];

    public static string Write(IEnumerable<FormField> fields) => JsonSerializer.Serialize(fields, Options);
}

internal static class FormMapping
{
    public static async Task<FormDetailDto> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var form = await db.Forms.AsNoTracking().FirstAsync(f => f.Id == id, ct);
        var versions = await db.FormVersions.AsNoTracking().Where(v => v.FormId == id).OrderBy(v => v.Version).ToListAsync(ct);
        var latest = versions.Last();
        var published = versions.FirstOrDefault(v => v.Version == form.PublishedVersion);
        var count = await db.FormSubmissions.CountAsync(s => s.FormId == id, ct);
        return new FormDetailDto(
            ToDto(form, count), latest.Version, latest.PublishedAt is null && latest.FieldsJson != "[]",
            FormJson.Fields(latest.FieldsJson), published is null ? [] : FormJson.Fields(published.FieldsJson));
    }

    public static FormDto ToDto(Form f, int submissions) => new(f.Id, f.Name, f.Description, f.Type, f.Status, f.IsPublic, f.PublishedVersion, submissions, f.CreatedAt);

    /// <summary>Looks up a form for filling in. A centre code means an anonymous visitor, who may only reach public forms.</summary>
    public static async Task<(Form? Form, Error? Problem)> ResolveAsync(IAppDbContext db, ITenantContext tenant, Guid id, string? centre, CancellationToken ct)
    {
        if (centre is not null)
        {
            var slug = centre.Trim().ToLowerInvariant();
            var found = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => !t.IsDeleted && t.Status == "active" && t.Slug == slug, ct);
            if (found is null)
            {
                return (null, Error.NotFound("This form is not available."));
            }

            tenant.TenantId = found.Id;
        }

        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == id && f.Status == FormStatuses.Live && f.PublishedVersion > 0 && (centre == null || f.IsPublic), ct);
        return form is null ? (null, Error.NotFound("This form is not available.")) : (form, null);
    }
}

public sealed class ListFormsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListFormsQuery, Result<CursorPage<FormDto>>>
{
    public async Task<Result<CursorPage<FormDto>>> Handle(ListFormsQuery request, CancellationToken ct)
    {
        var q = db.Forms.AsNoTracking().AsQueryable();
        if (!current.HasPermission(NMS.Application.Security.Permissions.FormsWrite))
        {
            q = q.Where(f => f.Status == FormStatuses.Live && f.PublishedVersion > 0);
        }
        else if (request.Status is not null)
        {
            q = q.Where(f => f.Status == request.Status);
        }
        else
        {
            q = q.Where(f => f.Status != FormStatuses.Archived);
        }

        if (request.Type is not null)
        {
            q = q.Where(f => f.Type == request.Type);
        }

        return Result<CursorPage<FormDto>>.Success(await q.ToCursorPageAsync(request.Cursor, request.Limit,
            f => new FormDto(f.Id, f.Name, f.Description, f.Type, f.Status, f.IsPublic, f.PublishedVersion, db.FormSubmissions.Count(s => s.FormId == f.Id), f.CreatedAt), ct));
    }
}

public sealed class GetFormHandler(IAppDbContext db) : IRequestHandler<GetFormQuery, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(GetFormQuery request, CancellationToken ct) =>
        await db.Forms.AnyAsync(f => f.Id == request.Id, ct) ? Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, request.Id, ct)) : Error.NotFound("Form not found.");
}

public sealed class CreateFormHandler(IAppDbContext db) : IRequestHandler<CreateFormCommand, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(CreateFormCommand request, CancellationToken ct)
    {
        var form = new Form { Name = request.Name, Description = request.Description, Type = request.Type, IsPublic = request.IsPublic };
        db.Forms.Add(form);
        db.FormVersions.Add(new FormVersion { FormId = form.Id, Version = 1, FieldsJson = "[]" });
        await db.SaveChangesAsync(ct);
        return Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, form.Id, ct));
    }
}

public sealed class UpdateFormHandler(IAppDbContext db) : IRequestHandler<UpdateFormCommand, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(UpdateFormCommand request, CancellationToken ct)
    {
        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (form is null)
        {
            return Error.NotFound("Form not found.");
        }

        form.Name = request.Name;
        form.Description = request.Description;
        form.Type = request.Type;
        form.IsPublic = request.IsPublic;
        await db.SaveChangesAsync(ct);
        return Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, form.Id, ct));
    }
}

public sealed class SaveFieldsHandler(IAppDbContext db) : IRequestHandler<SaveFieldsCommand, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(SaveFieldsCommand request, CancellationToken ct)
    {
        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (form is null)
        {
            return Error.NotFound("Form not found.");
        }

        var latest = await db.FormVersions.Where(v => v.FormId == form.Id).OrderByDescending(v => v.Version).FirstAsync(ct);
        var json = FormJson.Write(request.Fields);
        if (latest.PublishedAt is null)
        {
            latest.FieldsJson = json;
        }
        else
        {
            // The published version stays exactly as people saw it; changes go into a new draft.
            db.FormVersions.Add(new FormVersion { FormId = form.Id, Version = latest.Version + 1, FieldsJson = json });
        }

        await db.SaveChangesAsync(ct);
        return Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, form.Id, ct));
    }
}

public sealed class PublishFormHandler(IAppDbContext db, IClock clock) : IRequestHandler<PublishFormCommand, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(PublishFormCommand request, CancellationToken ct)
    {
        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (form is null)
        {
            return Error.NotFound("Form not found.");
        }

        var latest = await db.FormVersions.Where(v => v.FormId == form.Id).OrderByDescending(v => v.Version).FirstAsync(ct);
        if (latest.PublishedAt is not null)
        {
            return Error.InvalidState("There are no changes to publish.");
        }

        if (!FormJson.Fields(latest.FieldsJson).Any(f => FieldTypes.CollectsAnswer(f.Type)))
        {
            return Error.Validation("Add at least one question before publishing.", "fields");
        }

        latest.PublishedAt = clock.UtcNow;
        form.PublishedVersion = latest.Version;
        form.Status = FormStatuses.Live;
        await db.SaveChangesAsync(ct);
        return Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, form.Id, ct));
    }
}

public sealed class SetFormArchivedHandler(IAppDbContext db) : IRequestHandler<SetFormArchivedCommand, Result<FormDetailDto>>
{
    public async Task<Result<FormDetailDto>> Handle(SetFormArchivedCommand request, CancellationToken ct)
    {
        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (form is null)
        {
            return Error.NotFound("Form not found.");
        }

        form.Status = request.Archived ? FormStatuses.Archived : form.PublishedVersion > 0 ? FormStatuses.Live : FormStatuses.Draft;
        await db.SaveChangesAsync(ct);
        return Result<FormDetailDto>.Success(await FormMapping.DetailAsync(db, form.Id, ct));
    }
}

public sealed class GetFillableFormHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<GetFillableFormQuery, Result<FillableFormDto>>
{
    public async Task<Result<FillableFormDto>> Handle(GetFillableFormQuery request, CancellationToken ct)
    {
        var (form, problem) = await FormMapping.ResolveAsync(db, tenant, request.Id, request.Centre, ct);
        if (form is null)
        {
            return problem!;
        }

        var version = await db.FormVersions.AsNoTracking().FirstAsync(v => v.FormId == form.Id && v.Version == form.PublishedVersion, ct);
        return Result<FillableFormDto>.Success(new FillableFormDto(form.Id, form.Name, form.Description, form.Type, version.Version, FormJson.Fields(version.FieldsJson)));
    }
}

public sealed class StartFormHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<StartFormCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartFormCommand request, CancellationToken ct)
    {
        var (form, problem) = await FormMapping.ResolveAsync(db, tenant, request.Id, request.Centre, ct);
        if (form is null)
        {
            return problem!;
        }

        var start = new FormStart { FormId = form.Id, Version = form.PublishedVersion };
        db.FormStarts.Add(start);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(start.Id);
    }
}

public sealed class FormProgressHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<FormProgressCommand, Result>
{
    public async Task<Result> Handle(FormProgressCommand request, CancellationToken ct)
    {
        var (form, problem) = await FormMapping.ResolveAsync(db, tenant, request.Id, request.Centre, ct);
        if (form is null)
        {
            return Result.Failure(problem!);
        }

        var start = await db.FormStarts.FirstOrDefaultAsync(s => s.Id == request.StartId && s.FormId == form.Id, ct);
        if (start is null)
        {
            return Result.Failure(Error.NotFound("This form session was not found."));
        }

        if (!start.Submitted && request.FieldId.Length <= 60)
        {
            start.LastFieldId = request.FieldId;
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}

/// <summary>Checks answers against the published questions: required ones that are visible, the right kind of value, and only known fields.</summary>
public static class AnswerChecker
{
    public static bool IsVisible(FormField field, IReadOnlyDictionary<string, JsonElement> answers)
    {
        if (field.ShowWhenFieldId is null)
        {
            return true;
        }

        return answers.TryGetValue(field.ShowWhenFieldId, out var value) && AsText(value) == field.ShowWhenEquals;
    }

    public static string? AsText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };

    public static (Dictionary<string, JsonElement> Clean, List<Error> Problems) Check(IReadOnlyList<FormField> fields, IReadOnlyDictionary<string, JsonElement> answers, Func<Guid, bool> mediaExists)
    {
        var problems = new List<Error>();
        var clean = new Dictionary<string, JsonElement>();
        foreach (var key in answers.Keys.Where(k => fields.All(f => f.Id != k)))
        {
            problems.Add(Error.Validation("This form has no question with that id.", $"answers.{key}"));
        }

        foreach (var field in fields.Where(f => FieldTypes.CollectsAnswer(f.Type)))
        {
            if (!IsVisible(field, answers))
            {
                continue;
            }

            var has = answers.TryGetValue(field.Id, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) && !(value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));
            if (!has || (field.Type == "checkbox" && field.Required && value.ValueKind == JsonValueKind.False))
            {
                if (field.Required)
                {
                    problems.Add(Error.Validation("This question needs an answer.", $"answers.{field.Id}"));
                }

                continue;
            }

            var ok = field.Type switch
            {
                "text" => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 500,
                "textarea" => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 5000,
                "number" => value.ValueKind == JsonValueKind.Number || (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out _)),
                "date" => value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", out _),
                "dropdown" or "radio" => value.ValueKind == JsonValueKind.String && field.Options.Any(o => o.Value == value.GetString()),
                "checkbox" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "file" => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var media) && mediaExists(media),
                "signature" => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 400_000,
                _ => false,
            };
            if (!ok)
            {
                problems.Add(Error.Validation("That answer does not fit this question.", $"answers.{field.Id}"));
                continue;
            }

            clean[field.Id] = value;
        }

        return (clean, problems);
    }
}

public sealed class SubmitFormHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, IAccessScope scope, ICaptchaVerifier captcha, IClock clock)
    : IRequestHandler<SubmitFormCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SubmitFormCommand request, CancellationToken ct)
    {
        if (request.Centre is not null && !await captcha.VerifyAsync(request.CaptchaToken, request.IpAddress, ct))
        {
            return Error.Validation("Please confirm you are not a robot and try again.", "captcha_token");
        }

        var (form, problem) = await FormMapping.ResolveAsync(db, tenant, request.Id, request.Centre, ct);
        if (form is null)
        {
            return problem!;
        }

        var version = await db.FormVersions.AsNoTracking().FirstAsync(v => v.FormId == form.Id && v.Version == form.PublishedVersion, ct);
        var fields = FormJson.Fields(version.FieldsJson);
        var mediaIds = request.Answers.Values.Select(v => v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        var existing = (await db.MediaFiles.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        var (clean, problems) = AnswerChecker.Check(fields, request.Answers, existing.Contains);
        if (problems.Count > 0)
        {
            return Result<Guid>.Failure(problems);
        }

        if (request.Centre is not null && (request.StudentId is not null || request.EnquiryId is not null))
        {
            return Error.Validation("Public forms cannot be linked to a child or enquiry.", "student_id");
        }

        if (request.StudentId is { } sid)
        {
            var own = await scope.GetOwnStudentIdsAsync(ct);
            if (own is not null ? !own.Contains(sid) : !await db.Students.AnyAsync(s => s.Id == sid, ct))
            {
                return Error.Validation("Choose one of your own children.", "student_id");
            }
        }

        if (request.EnquiryId is { } eid && !await db.Enquiries.AnyAsync(e => e.Id == eid, ct))
        {
            return Error.Validation("The enquiry does not exist.", "enquiry_id");
        }

        FormStart? start = null;
        if (request.StartId is { } stId)
        {
            start = await db.FormStarts.FirstOrDefaultAsync(s => s.Id == stId && s.FormId == form.Id, ct);
        }

        var now = clock.UtcNow;
        var submission = new FormSubmission
        {
            FormId = form.Id, Version = version.Version, StudentId = request.StudentId, EnquiryId = request.EnquiryId, StartId = start?.Id,
            SubmittedByUserId = current.IsAuthenticated ? current.UserId : null, AnswersJson = JsonSerializer.Serialize(clean, FormJson.Options),
            DurationSeconds = start is null ? null : (int)Math.Min(86_400, Math.Max(0, (now - start.CreatedAt).TotalSeconds)),
        };
        db.FormSubmissions.Add(submission);
        if (start is not null)
        {
            start.Submitted = true;
            start.SubmittedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(submission.Id);
    }
}

public sealed class ListSubmissionsHandler(IAppDbContext db) : IRequestHandler<ListSubmissionsQuery, Result<CursorPage<SubmissionDto>>>
{
    public async Task<Result<CursorPage<SubmissionDto>>> Handle(ListSubmissionsQuery request, CancellationToken ct)
    {
        if (!await db.Forms.AnyAsync(f => f.Id == request.Id, ct))
        {
            return Error.NotFound("Form not found.");
        }

        var page = await db.FormSubmissions.AsNoTracking().Where(s => s.FormId == request.Id).ToCursorPageAsync(request.Cursor, request.Limit,
            s => new SubmissionDto(s.Id, s.Version, s.StudentId, db.Students.Where(x => x.Id == s.StudentId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault(), s.EnquiryId, new Dictionary<string, JsonElement>(), s.DurationSeconds, s.CreatedAt), ct);

        // Answers are encrypted in the database, so they are read and decoded here, one page at a time.
        var ids = page.Items.Select(i => i.Id).ToList();
        var answers = await db.FormSubmissions.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.AnswersJson }).ToDictionaryAsync(s => s.Id, s => s.AnswersJson, ct);
        var items = page.Items.Select(i => i with { Answers = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(answers.GetValueOrDefault(i.Id) ?? "{}", FormJson.Options) ?? [] }).ToList();
        return Result<CursorPage<SubmissionDto>>.Success(new CursorPage<SubmissionDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }
}

public sealed class FormAnalyticsHandler(IAppDbContext db) : IRequestHandler<FormAnalyticsQuery, Result<FormAnalyticsDto>>
{
    public async Task<Result<FormAnalyticsDto>> Handle(FormAnalyticsQuery request, CancellationToken ct)
    {
        if (!await db.Forms.AnyAsync(f => f.Id == request.Id, ct))
        {
            return Error.NotFound("Form not found.");
        }

        var starts = db.FormStarts.AsNoTracking().Where(s => s.FormId == request.Id);
        var subs = db.FormSubmissions.AsNoTracking().Where(s => s.FormId == request.Id);
        if (request.From is { } from)
        {
            var f = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            starts = starts.Where(s => s.CreatedAt >= f);
            subs = subs.Where(s => s.CreatedAt >= f);
        }

        if (request.To is { } to)
        {
            var t = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            starts = starts.Where(s => s.CreatedAt < t);
            subs = subs.Where(s => s.CreatedAt < t);
        }

        var startCount = await starts.CountAsync(ct);
        var finished = await starts.CountAsync(s => s.Submitted, ct);
        var submissionCount = await subs.CountAsync(ct);
        var average = await subs.Where(s => s.DurationSeconds != null).AverageAsync(s => (double?)s.DurationSeconds, ct) ?? 0;
        var dropRows = await starts.Where(s => !s.Submitted && s.LastFieldId != null).GroupBy(s => s.LastFieldId!).Select(g => new { Field = g.Key, N = g.Count() }).OrderByDescending(d => d.N).Take(5).ToListAsync(ct);
        var drops = dropRows.Select(d => new DropOff(d.Field, d.N)).ToList();
        return Result<FormAnalyticsDto>.Success(new FormAnalyticsDto(startCount, submissionCount, startCount == 0 ? 0 : Math.Round(finished * 100.0 / startCount, 1), Math.Round(average, 1), drops));
    }
}
