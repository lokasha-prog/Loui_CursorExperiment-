using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Newsletters;
using NMS.Application.Resources;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record ScheduleRequest(DateTimeOffset At);

public sealed record FolderRequest(Dictionary<string, string> Name, string Audience, Guid? ParentFolderId);

public sealed record DocumentRequest(string Title, string? Description, string? Tags, Guid? FolderId);

public static class NewsletterEndpoints
{
    public static void MapNewsletterEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/newsletters").WithTags("Newsletters");
        g.MapGet("/", async (string? status, string? search, [FromQuery(Name = "created_by")] Guid? createdBy, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListNewslettersQuery(status, search, createdBy, cursor, limit), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetNewsletterQuery(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/", async (NewsletterFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveNewsletterCommand(null, body), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPatch("/{id:guid}", async (Guid id, NewsletterFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveNewsletterCommand(id, body), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteNewsletterCommand(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/{id:guid}/actions/schedule", async (Guid id, ScheduleRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ScheduleNewsletterCommand(id, body.At), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/{id:guid}/actions/unschedule", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new UnscheduleNewsletterCommand(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/{id:guid}/actions/publish", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PublishNewsletterCommand(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/{id:guid}/actions/archive", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ArchiveNewsletterCommand(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapGet("/{id:guid}/stats", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new NewsletterStatsQuery(id), ct))).RequireAuthorization(Permissions.MessagingBroadcast);
        g.MapPost("/actions/draft", async (string? language, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DraftNewsletterCommand(language ?? "en"), ct))).RequireAuthorization(Permissions.MessagingBroadcast);

        g.MapGet("/mine", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new MyNewslettersQuery(cursor, limit), ct))).RequireAuthorization(Permissions.MessagingRead);
        g.MapGet("/{id:guid}/read", async (Guid id, string? language, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ReadNewsletterCommand(id, language ?? "en"), ct))).RequireAuthorization(Permissions.MessagingRead);

        routes.MapGet("/api/v1/public/newsletters/click/{token}/{index:int}", async (string token, int index, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new ClickNewsletterCommand(token, index), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.Redirect(result.Value);
        }).WithTags("Public").AllowAnonymous().RequireRateLimiting("anonymous");
    }
}

public static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/resources").WithTags("Resources");
        g.MapGet("/folders", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListFoldersQuery(), ct))).RequireAuthorization(Permissions.ResourcesRead);
        g.MapPost("/folders", async (FolderRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveFolderCommand(null, body.Name, body.Audience, body.ParentFolderId), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapPatch("/folders/{id:guid}", async (Guid id, FolderRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveFolderCommand(id, body.Name, body.Audience, body.ParentFolderId), ct))).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapDelete("/folders/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteFolderCommand(id), ct))).RequireAuthorization(Permissions.ResourcesWrite);

        g.MapGet("/documents", async ([FromQuery(Name = "folder_id")] Guid? folderId, string? search, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListDocumentsQuery(folderId, search, cursor, limit), ct))).RequireAuthorization(Permissions.ResourcesRead);
        g.MapPost("/documents", async ([FromForm(Name = "folder_id")] Guid folderId, [FromForm] string title, [FromForm] string? description, [FromForm] string? tags, IFormFile file, HttpContext http, ISender s, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return ApiResults.From(http, await s.Send(new UploadDocumentCommand(folderId, title, description, tags, file.FileName, buffer.ToArray()), ct), StatusCodes.Status201Created);
        }).RequireAuthorization(Permissions.ResourcesWrite).DisableAntiforgery();
        g.MapPatch("/documents/{id:guid}", async (Guid id, DocumentRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new UpdateDocumentCommand(id, body.Title, body.Description, body.Tags, body.FolderId), ct))).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapDelete("/documents/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteDocumentCommand(id), ct))).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapPost("/documents/{id:guid}/versions", async (Guid id, IFormFile file, HttpContext http, ISender s, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return ApiResults.From(http, await s.Send(new UploadVersionCommand(id, file.FileName, buffer.ToArray()), ct), StatusCodes.Status201Created);
        }).RequireAuthorization(Permissions.ResourcesWrite).DisableAntiforgery();
        g.MapGet("/documents/{id:guid}/versions", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListVersionsQuery(id), ct))).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapGet("/documents/{id:guid}/downloads", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListDownloadsQuery(id), ct))).RequireAuthorization(Permissions.ResourcesWrite);
        g.MapGet("/documents/{id:guid}/download", async (Guid id, int? version, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DownloadDocumentQuery(id, version), ct);
            if (result.IsFailure)
            {
                return ApiResults.Failure(http, result.Errors);
            }

            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).RequireAuthorization(Permissions.ResourcesRead);
    }
}
