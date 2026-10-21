using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Ai;
using NMS.Shared.Results;

namespace NMS.Application.Learning;

public sealed record MilestoneSuggestionDto(Guid MilestoneId, string Code, Dictionary<string, string> Statement, Guid AreaId, Guid AgeBandId, int Score);

/// <summary>Suggests milestones for an observation by matching its words with the curriculum. Runs entirely inside the system; no text is sent to any outside service.</summary>
public sealed record SuggestMilestonesQuery(Guid FrameworkId, string Text, Guid? AreaId, Guid? AgeBandId) : IRequest<Result<IReadOnlyList<MilestoneSuggestionDto>>>;

public sealed class SuggestMilestonesHandler(IAppDbContext db) : IRequestHandler<SuggestMilestonesQuery, Result<IReadOnlyList<MilestoneSuggestionDto>>>
{
    private static readonly HashSet<string> Stop = ["the", "a", "an", "and", "of", "to", "in", "with", "on", "for", "is", "was", "he", "she", "they", "her", "his", "their", "at", "it", "that", "this", "as", "by", "from"];

    internal static HashSet<string> Words(string text) =>
        text.ToLowerInvariant().Split([' ', ',', '.', ';', ':', '!', '?', '-', '\n', '\r', '(', ')', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !Stop.Contains(w)).Select(w => w.TrimEnd('s')).ToHashSet();

    public async Task<Result<IReadOnlyList<MilestoneSuggestionDto>>> Handle(SuggestMilestonesQuery request, CancellationToken ct)
    {
        var words = Words(request.Text);
        if (words.Count == 0)
        {
            return Result<IReadOnlyList<MilestoneSuggestionDto>>.Success([]);
        }

        var query = db.Milestones.AsNoTracking().Where(m => m.FrameworkId == request.FrameworkId);
        if (request.AreaId is { } a) query = query.Where(m => m.AreaId == a);
        if (request.AgeBandId is { } b) query = query.Where(m => m.AgeBandId == b);
        var rows = await query.ToListAsync(ct);
        var scored = rows.Select(m => new { m, score = m.Statement.Values.SelectMany(v => Words(v)).Distinct().Count(w => words.Contains(w)) })
            .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenBy(x => x.m.Code).Take(5)
            .Select(x => new MilestoneSuggestionDto(x.m.Id, x.m.Code, x.m.Statement, x.m.AreaId, x.m.AgeBandId, x.score)).ToList();
        return Result<IReadOnlyList<MilestoneSuggestionDto>>.Success(scored);
    }
}

public sealed record LessonDraftDto(Dictionary<string, string> Title, Dictionary<string, string> Description, IReadOnlyList<string> Materials, IReadOnlyList<string> Objectives, string Language, string Model);

/// <summary>Asks the AI Brain for a lesson idea. Only the curriculum area, age band name and an optional theme are sent: no child or staff data.</summary>
public sealed record LessonDraftQuery(Guid FrameworkId, Guid? AreaId, Guid? AgeBandId, string? Theme, string Language) : IRequest<Result<LessonDraftDto>>;

public sealed class LessonDraftValidator : AbstractValidator<LessonDraftQuery>
{
    public LessonDraftValidator()
    {
        RuleFor(q => q.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
        RuleFor(q => q.Theme).MaximumLength(120);
    }
}

public sealed class LessonDraftHandler(IAppDbContext db, IAiBrain brain) : IRequestHandler<LessonDraftQuery, Result<LessonDraftDto>>
{
    public async Task<Result<LessonDraftDto>> Handle(LessonDraftQuery request, CancellationToken ct)
    {
        if (!brain.IsConfigured)
        {
            return Error.InvalidState("The AI Brain is not set up yet.");
        }

        var framework = await db.CurriculumFrameworks.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FrameworkId, ct);
        if (framework is null)
        {
            return Error.Validation("The curriculum was not found.", "framework_id");
        }

        string Name(Dictionary<string, string>? n) => n is null ? string.Empty : n.GetValueOrDefault("en") ?? n.Values.FirstOrDefault() ?? string.Empty;
        var area = request.AreaId is { } a ? await db.DevelopmentAreas.AsNoTracking().Where(x => x.Id == a && x.FrameworkId == framework.Id).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var band = request.AgeBandId is { } b ? await db.AgeBands.AsNoTracking().Where(x => x.Id == b && x.FrameworkId == framework.Id).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;

        var prompt = new StringBuilder();
        prompt.AppendLine($"Curriculum: {Name(framework.Name)}");
        if (area is not null) prompt.AppendLine($"Development area: {Name(area)}");
        if (band is not null) prompt.AppendLine($"Age band: {Name(band)}");
        if (!string.IsNullOrWhiteSpace(request.Theme)) prompt.AppendLine($"Theme: {request.Theme.Trim()}");

        var system = "You help nursery teachers in the UAE plan short play-based lessons. Write ONE lesson idea. Reply in exactly this plain format, nothing else:\n"
            + "TITLE: <short title>\nDESCRIPTION: <two sentences on what the children do>\nMATERIALS: <item; item; item>\nOBJECTIVES: <objective; objective; objective>\n"
            + "Use simple, safe, low-cost activities. No markdown. Write the text in " + (request.Language == "ar" ? "Arabic (Modern Standard Arabic)." : "English.");
        var answer = await brain.CompleteAsync(system, prompt.ToString(), ct);
        if (answer.IsFailure)
        {
            return Result<LessonDraftDto>.Failure(answer.Errors);
        }

        string Field(string label)
        {
            foreach (var line in answer.Value.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase)) return t[(label.Length + 1)..].Trim();
            }

            return string.Empty;
        }

        IReadOnlyList<string> List(string label) => Field(label).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(10).ToList();
        var title = Field("TITLE");
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.InvalidState("The AI Brain did not return a usable lesson. Please try again.");
        }

        return Result<LessonDraftDto>.Success(new LessonDraftDto(
            new() { [request.Language] = title }, new() { [request.Language] = Field("DESCRIPTION") }, List("MATERIALS"), List("OBJECTIVES"), request.Language, brain.ModelName));
    }
}
