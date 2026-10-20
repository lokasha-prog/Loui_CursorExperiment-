using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Enquiries;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record ChangeStageRequest(string Stage, string? Reason);

public sealed record ActivityRequest(string Kind, string Summary);

public sealed record ConvertRequest(Guid? RoomId, DateOnly? StartDate);

public static class EnquiryEndpoints
{
    public static void MapEnquiryEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/enquiries").WithTags("Enquiries");
        g.MapGet("/", async (string? stage, string? source, [FromQuery(Name = "staff_id")] Guid? staffId, DateOnly? from, DateOnly? to, bool? archived, string? search, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListEnquiriesQuery(stage, source, staffId, from, to, archived ?? false, search, cursor, limit), ct))).RequireAuthorization(Permissions.EnquiriesRead);
        g.MapGet("/summary", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new EnquirySummaryQuery(), ct))).RequireAuthorization(Permissions.EnquiriesRead);
        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetEnquiryQuery(id), ct))).RequireAuthorization(Permissions.EnquiriesRead);
        g.MapPost("/", async (EnquiryFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveEnquiryCommand(null, body), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPatch("/{id:guid}", async (Guid id, EnquiryFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveEnquiryCommand(id, body), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPost("/{id:guid}/actions/change-stage", async (Guid id, ChangeStageRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ChangeStageCommand(id, body.Stage, body.Reason), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPost("/{id:guid}/activities", async (Guid id, ActivityRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AddActivityCommand(id, body.Kind, body.Summary), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPost("/{id:guid}/actions/archive", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ArchiveEnquiryCommand(id, true), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPost("/{id:guid}/actions/unarchive", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ArchiveEnquiryCommand(id, false), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
        g.MapPost("/{id:guid}/actions/convert", async (Guid id, ConvertRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ConvertEnquiryCommand(id, body?.RoomId, body?.StartDate), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
    }
}

public sealed record TourStatusRequest(string Status);

public sealed record BlockOffRequest(Guid? StaffId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

public static class TourEndpoints
{
    public static void MapTourEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/tours").WithTags("Tours");
        g.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? to, [FromQuery(Name = "staff_id")] Guid? staffId, string? status, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListToursQuery(from, to, staffId, status), ct))).RequireAuthorization(Permissions.ToursRead);
        g.MapPost("/", async (TourFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveTourCommand(null, body), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.ToursWrite);
        g.MapPatch("/{id:guid}", async (Guid id, TourFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveTourCommand(id, body), ct))).RequireAuthorization(Permissions.ToursWrite);
        g.MapPost("/{id:guid}/actions/set-status", async (Guid id, TourStatusRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetTourStatusCommand(id, body.Status), ct))).RequireAuthorization(Permissions.ToursWrite);
        g.MapGet("/block-offs", async (DateTimeOffset? from, DateTimeOffset? to, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListBlockOffsQuery(from, to), ct))).RequireAuthorization(Permissions.ToursRead);
        g.MapPost("/block-offs", async (BlockOffRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AddBlockOffCommand(body.StaffId, body.StartsAt, body.EndsAt, body.Reason), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.ToursWrite);
        g.MapDelete("/block-offs/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteBlockOffCommand(id), ct))).RequireAuthorization(Permissions.ToursWrite);

        var tasks = routes.MapGroup("/api/v1/follow-up-tasks").WithTags("Tours");
        tasks.MapGet("/", async (bool? done, [FromQuery(Name = "staff_id")] Guid? staffId, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListTasksQuery(done ?? false, staffId, cursor, limit), ct))).RequireAuthorization(Permissions.EnquiriesRead);
        tasks.MapPost("/{id:guid}/actions/complete", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CompleteTaskCommand(id), ct))).RequireAuthorization(Permissions.EnquiriesWrite);
    }
}
