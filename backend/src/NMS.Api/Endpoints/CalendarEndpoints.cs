using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Calendar;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/calendar").WithTags("Calendar");
        g.MapGet("/events", async (DateOnly from, DateOnly to, [FromQuery(Name = "room_id")] Guid? roomId, string? type, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListEventsQuery(from, to, roomId, type), ct))).RequireAuthorization(Permissions.CalendarRead);
        g.MapGet("/events/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetEventQuery(id), ct))).RequireAuthorization(Permissions.CalendarRead);
        g.MapPost("/events", async (EventFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveEventCommand(null, body), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.CalendarWrite);
        g.MapPatch("/events/{id:guid}", async (Guid id, EventFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveEventCommand(id, body), ct))).RequireAuthorization(Permissions.CalendarWrite);
        g.MapDelete("/events/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteEventCommand(id), ct))).RequireAuthorization(Permissions.CalendarWrite);
        g.MapGet("/agenda", async (DateOnly from, DateOnly to, string? layers, bool? mine, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AgendaQuery(from, to, (layers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), mine ?? false), ct)))
            .RequireAuthorization(Permissions.CalendarRead);
        g.MapGet("/schedules", async ([FromQuery(Name = "room_id")] Guid? roomId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ScheduleQuery(roomId), ct))).RequireAuthorization(Permissions.StaffRead);
        g.MapGet("/feed-link", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFeedLinkQuery(false), ct))).RequireAuthorization(Permissions.CalendarRead);
        g.MapPost("/feed-link/actions/rotate", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFeedLinkQuery(true), ct))).RequireAuthorization(Permissions.SettingsWrite);

        routes.MapGet("/api/v1/public/calendar/{centre}/{token}", async (string centre, string token, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new IcsFeedQuery(centre, token.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) ? token[..^4] : token), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.Text(result.Value, "text/calendar; charset=utf-8");
        }).WithTags("Public").AllowAnonymous().RequireRateLimiting("anonymous");
    }
}
