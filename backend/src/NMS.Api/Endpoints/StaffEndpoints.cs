using MediatR;
using NMS.Api.Http;
using NMS.Application.Security;
using NMS.Application.StaffMembers;

namespace NMS.Api.Endpoints;

public sealed record RoomAssignmentRequest(
    Guid RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/staff").WithTags("Staff");

        g.MapGet("/", async (
            string? cursor, int? limit, string? status, string? search, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListStaffQuery(cursor, limit, status, search), ct)))
            .RequireAuthorization(Permissions.StaffRead);

        g.MapPost("/", async (CreateStaffCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.StaffWrite);

        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetStaffQuery(id), ct)))
            .RequireAuthorization(Permissions.StaffRead);

        g.MapPatch("/{id:guid}", async (Guid id, UpdateStaffCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.StaffWrite);

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteStaffCommand(id), ct)))
            .RequireAuthorization(Permissions.StaffWrite);

        g.MapGet("/{id:guid}/room-assignments", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListRoomAssignmentsQuery(id), ct)))
            .RequireAuthorization(Permissions.StaffWrite);

        g.MapPost("/{id:guid}/room-assignments", async (
            Guid id, RoomAssignmentRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(
                http,
                await s.Send(new CreateRoomAssignmentCommand(
                    id, body.RoomId, body.DayOfWeek, body.StartTime, body.EndTime, body.EffectiveFrom, body.EffectiveTo), ct),
                StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.StaffWrite);
    }
}
