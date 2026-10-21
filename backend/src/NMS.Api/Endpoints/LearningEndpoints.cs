using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Learning;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record FrameworkRequest(string Code, Dictionary<string, string> Name, Dictionary<string, string>? Description);

public sealed record ImportRequest(IReadOnlyList<ImportArea>? Areas, IReadOnlyList<ImportBand>? AgeBands, IReadOnlyList<ImportMilestone>? Milestones);

public sealed record AreaRequest(string Code, Dictionary<string, string> Name, int? SortOrder);

public sealed record BandRequest(string Code, Dictionary<string, string> Name, int MinMonths, int MaxMonths);

public sealed record MilestoneRequest(Guid AreaId, Guid AgeBandId, string Code, Dictionary<string, string> Statement);

public sealed record LessonRequest(
    Dictionary<string, string> Title, Dictionary<string, string>? Description, Guid FrameworkId, Guid? AreaId, Guid? AgeBandId, Guid? RoomId, int? DurationMinutes,
    IReadOnlyList<string>? Materials, IReadOnlyList<string>? Objectives, Dictionary<string, string>? Links, IReadOnlyList<Guid>? MilestoneIds, bool? IsTemplate);

/// <summary>Learning module, part one: the curriculum a centre follows (its own data) and the lessons that use it.</summary>
public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder routes)
    {
        var f = routes.MapGroup("/api/v1/learning/frameworks").WithTags("Learning");
        f.MapGet("/", async (HttpContext http, ISender s, CancellationToken ct) => ApiResults.From(http, await s.Send(new ListFrameworksQuery(), ct))).RequireAuthorization(Permissions.LearningRead);
        f.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) => ApiResults.From(http, await s.Send(new GetFrameworkQuery(id), ct))).RequireAuthorization(Permissions.LearningRead);
        f.MapPost("/", async (FrameworkRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveFrameworkCommand(null, b.Code, b.Name, b.Description), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPut("/{id:guid}", async (Guid id, FrameworkRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveFrameworkCommand(id, b.Code, b.Name, b.Description), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DeleteFrameworkCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.NoContent();
        }).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPost("/{id:guid}/import", async (Guid id, ImportRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ImportFrameworkCommand(id, b.Areas, b.AgeBands, b.Milestones), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPost("/{id:guid}/areas", async (Guid id, AreaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveAreaCommand(id, null, b.Code, b.Name, b.SortOrder), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPut("/{id:guid}/areas/{partId:guid}", async (Guid id, Guid partId, AreaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveAreaCommand(id, partId, b.Code, b.Name, b.SortOrder), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPost("/{id:guid}/age-bands", async (Guid id, BandRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveBandCommand(id, null, b.Code, b.Name, b.MinMonths, b.MaxMonths), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPut("/{id:guid}/age-bands/{partId:guid}", async (Guid id, Guid partId, BandRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveBandCommand(id, partId, b.Code, b.Name, b.MinMonths, b.MaxMonths), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPost("/{id:guid}/milestones", async (Guid id, MilestoneRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveMilestoneCommand(id, null, b.AreaId, b.AgeBandId, b.Code, b.Statement), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapPut("/{id:guid}/milestones/{partId:guid}", async (Guid id, Guid partId, MilestoneRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveMilestoneCommand(id, partId, b.AreaId, b.AgeBandId, b.Code, b.Statement), ct))).RequireAuthorization(Permissions.SettingsWrite);
        f.MapDelete("/{id:guid}/{kind}/{partId:guid}", async (Guid id, string kind, Guid partId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeletePartCommand(id, kind.Replace('-', '_').TrimEnd('s'), partId), ct))).RequireAuthorization(Permissions.SettingsWrite);

        routes.MapGet("/api/v1/learning/suggest-milestones", async ([FromQuery(Name = "framework_id")] Guid frameworkId, string text, [FromQuery(Name = "area_id")] Guid? areaId, [FromQuery(Name = "age_band_id")] Guid? ageBandId,
            HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SuggestMilestonesQuery(frameworkId, text ?? string.Empty, areaId, ageBandId), ct))).WithTags("Learning").RequireAuthorization(Permissions.LearningWrite);
        routes.MapGet("/api/v1/ai/lesson-draft", async ([FromQuery(Name = "framework_id")] Guid frameworkId, [FromQuery(Name = "area_id")] Guid? areaId, [FromQuery(Name = "age_band_id")] Guid? ageBandId,
            string? theme, string? language, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new LessonDraftQuery(frameworkId, areaId, ageBandId, theme, language ?? "en"), ct))).WithTags("AI Brain").RequireAuthorization(Permissions.LearningWrite);

        var l = routes.MapGroup("/api/v1/learning/lessons").WithTags("Learning");
        l.MapGet("/", async ([FromQuery(Name = "framework_id")] Guid? frameworkId, [FromQuery(Name = "area_id")] Guid? areaId, [FromQuery(Name = "age_band_id")] Guid? ageBandId,
            [FromQuery(Name = "room_id")] Guid? roomId, string? search, bool? templates, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListLessonsQuery(frameworkId, areaId, ageBandId, roomId, search, templates, cursor, limit), ct))).RequireAuthorization(Permissions.LearningRead);
        l.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) => ApiResults.From(http, await s.Send(new GetLessonQuery(id), ct))).RequireAuthorization(Permissions.LearningRead);
        l.MapPost("/", async (LessonRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(Save(null, b), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.LearningWrite);
        l.MapPut("/{id:guid}", async (Guid id, LessonRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(Save(id, b), ct))).RequireAuthorization(Permissions.LearningWrite);
        l.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DeleteLessonCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.NoContent();
        }).RequireAuthorization(Permissions.LearningWrite);
    }

    private static SaveLessonCommand Save(Guid? id, LessonRequest b) =>
        new(id, b.Title, b.Description, b.FrameworkId, b.AreaId, b.AgeBandId, b.RoomId, b.DurationMinutes ?? 30, b.Materials, b.Objectives, b.Links, b.MilestoneIds, b.IsTemplate ?? false);
}
