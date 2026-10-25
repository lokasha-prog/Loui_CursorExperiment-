using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Communication;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record DecideActivityRequest(string? Reason);

public sealed record NewThreadRequest(string Subject, string Body, Guid? StudentId, bool Broadcast, Guid? RoomId);

public sealed record ReplyRequest(string Body);

public static class CommunicationEndpoints
{
    public static void MapCommunicationEndpoints(this IEndpointRouteBuilder routes)
    {
        var media = routes.MapGroup("/api/v1/media").WithTags("Media");
        media.MapPost("/", async (IFormFile file, HttpContext http, ISender s, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return ApiResults.From(http, await s.Send(new UploadMediaCommand(file.FileName, buffer.ToArray()), ct), StatusCodes.Status201Created);
        }).RequireAuthorization().DisableAntiforgery();
        media.MapGet("/{id:guid}/content", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new GetMediaContentQuery(id), ct);
            if (result.IsFailure)
            {
                return ApiResults.Failure(http, result.Errors);
            }

            http.Response.Headers.CacheControl = "private, max-age=3600";
            http.Response.Headers["Content-Disposition"] = "inline";
            return Results.File(result.Value.Content, result.Value.ContentType);
        }).RequireAuthorization();

        var a = routes.MapGroup("/api/v1/activities").WithTags("Activities");
        a.MapGet("/", async (
            string? status, [FromQuery(Name = "student_id")] Guid? studentId, [FromQuery(Name = "with_media")] bool? withMedia,
            [FromQuery(Name = "include_shared")] bool? includeShared, string? cursor, int? limit,
            HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListActivitiesQuery(status, studentId, cursor, limit, withMedia, includeShared), ct)))
            .RequireAuthorization(Permissions.ActivitiesRead);
        a.MapPost("/", async (CreateActivityCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.ActivitiesWrite);
        a.MapPost("/centres", async (ShareWithCentresCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.GroupManage);
        a.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteActivityCommand(id), ct)))
            .RequireAuthorization(Permissions.ActivitiesWrite);
        a.MapPost("/{id:guid}/actions/approve", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DecideActivityCommand(id, true, null), ct)))
            .RequireAuthorization(Permissions.ActivitiesApprove);
        a.MapPost("/{id:guid}/actions/reject", async (Guid id, DecideActivityRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DecideActivityCommand(id, false, body?.Reason), ct)))
            .RequireAuthorization(Permissions.ActivitiesApprove);

        routes.MapGet("/api/v1/daily-reports", async ([FromQuery(Name = "student_id")] Guid? studentId, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListDailyReportsQuery(studentId, cursor, limit), ct)))
            .WithTags("Daily reports").RequireAuthorization(Permissions.ActivitiesRead);

        var m = routes.MapGroup("/api/v1/messages").WithTags("Messaging");
        m.MapGet("/threads", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListThreadsQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.MessagingRead);
        m.MapGet("/threads/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetThreadQuery(id), ct)))
            .RequireAuthorization(Permissions.MessagingRead);
        m.MapPost("/threads", async (NewThreadRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CreateThreadCommand(body.Subject, body.Body, body.StudentId, body.Broadcast, body.RoomId), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.MessagingWrite);
        m.MapPost("/threads/{id:guid}/messages", async (Guid id, ReplyRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ReplyCommand(id, body.Body), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.MessagingWrite);
    }
}
