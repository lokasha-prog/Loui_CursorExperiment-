using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Learning;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record ObservationRequest(
    Guid StudentId, DateTimeOffset? ObservedAt, string? Type, Guid? FrameworkId, Guid? AreaId, Guid? AgeBandId, IReadOnlyList<Guid>? MilestoneIds,
    Dictionary<string, string> Notes, IReadOnlyList<Guid>? MediaIds, bool? ShareWithParents);

public sealed record TemplateRequest(Dictionary<string, string> Name, Guid FrameworkId, Guid? AgeBandId, string? ScoringKind, IReadOnlyList<Guid>? MilestoneIds);

public sealed record StartRunRequest(Guid TemplateId, Guid StudentId, string Term, DateOnly? AssessedOn);

public sealed record ScoresRequest(IReadOnlyList<ScoreInput>? Scores, Dictionary<string, string>? StaffSummary, Dictionary<string, string>? ParentSummary);

/// <summary>Learning module, part two: observations, assessments with a parent version that needs approval, and the learning journey.</summary>
public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var o = routes.MapGroup("/api/v1/learning/observations").WithTags("Learning");
        o.MapGet("/", async ([FromQuery(Name = "student_id")] Guid? studentId, string? type, [FromQuery(Name = "area_id")] Guid? areaId, DateOnly? from, DateOnly? to, string? cursor, int? limit,
            HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListObservationsQuery(studentId, type, areaId, from, to, cursor, limit), ct))).RequireAuthorization(Permissions.LearningRead);
        o.MapPost("/", async (ObservationRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(Save(null, b), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.LearningWrite);
        o.MapPut("/{id:guid}", async (Guid id, ObservationRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(Save(id, b), ct))).RequireAuthorization(Permissions.LearningWrite);
        o.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DeleteObservationCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.NoContent();
        }).RequireAuthorization(Permissions.LearningWrite);

        var t = routes.MapGroup("/api/v1/learning/assessment-templates").WithTags("Learning");
        t.MapGet("/", async (HttpContext http, ISender s, CancellationToken ct) => ApiResults.From(http, await s.Send(new ListTemplatesQuery(), ct))).RequireAuthorization(Permissions.LearningRead);
        t.MapPost("/", async (TemplateRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveTemplateCommand(null, b.Name, b.FrameworkId, b.AgeBandId, b.ScoringKind ?? "three_point", b.MilestoneIds), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.LearningApprove);
        t.MapPut("/{id:guid}", async (Guid id, TemplateRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveTemplateCommand(id, b.Name, b.FrameworkId, b.AgeBandId, b.ScoringKind ?? "three_point", b.MilestoneIds), ct))).RequireAuthorization(Permissions.LearningApprove);
        t.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DeleteTemplateCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.NoContent();
        }).RequireAuthorization(Permissions.LearningApprove);

        var a = routes.MapGroup("/api/v1/learning/assessments").WithTags("Learning");
        a.MapGet("/", async ([FromQuery(Name = "student_id")] Guid? studentId, [FromQuery(Name = "template_id")] Guid? templateId, string? term, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListRunsQuery(studentId, templateId, term, cursor, limit), ct))).RequireAuthorization(Permissions.LearningRead);
        a.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) => ApiResults.From(http, await s.Send(new GetRunQuery(id), ct))).RequireAuthorization(Permissions.LearningRead);
        a.MapPost("/", async (StartRunRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new StartRunCommand(b.TemplateId, b.StudentId, b.Term, b.AssessedOn), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.LearningWrite);
        a.MapPut("/{id:guid}/scores", async (Guid id, ScoresRequest b, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveScoresCommand(id, b.Scores ?? [], b.StaffSummary, b.ParentSummary), ct))).RequireAuthorization(Permissions.LearningWrite);
        a.MapPost("/{id:guid}/actions/complete", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CompleteRunCommand(id), ct))).RequireAuthorization(Permissions.LearningWrite);
        a.MapPost("/{id:guid}/actions/approve", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ApproveRunCommand(id), ct))).RequireAuthorization(Permissions.LearningApprove);
        a.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DeleteRunCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.NoContent();
        }).RequireAuthorization(Permissions.LearningWrite);

        routes.MapGet("/api/v1/learning/journey/{studentId:guid}", async (Guid studentId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new JourneyQuery(studentId), ct))).WithTags("Learning").RequireAuthorization(Permissions.JourneyRead);
        routes.MapGet("/api/v1/learning/parent-reports/{studentId:guid}", async (Guid studentId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ParentReportsQuery(studentId), ct))).WithTags("Learning").RequireAuthorization(Permissions.JourneyRead);
    }

    private static SaveObservationCommand Save(Guid? id, ObservationRequest b) =>
        new(id, b.StudentId, b.ObservedAt, b.Type ?? "informal", b.FrameworkId, b.AreaId, b.AgeBandId, b.MilestoneIds, b.Notes, b.MediaIds, b.ShareWithParents ?? false);
}
