using MediatR;
using NMS.Api.Http;
using NMS.Application.Groups;
using NMS.Application.Labels;
using NMS.Application.Programs;
using NMS.Application.Rooms;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public static class CentreStructureEndpoints
{
    public static void MapCentreStructureEndpoints(this IEndpointRouteBuilder routes)
    {
        MapRooms(routes.MapGroup("/api/v1/rooms").WithTags("Rooms"));
        MapPrograms(routes.MapGroup("/api/v1/programs").WithTags("Programs"));
        MapGroups(routes.MapGroup("/api/v1/groups").WithTags("Groups"));
        MapLabels(routes.MapGroup("/api/v1/labels").WithTags("Labels"));
    }

    private static void MapRooms(RouteGroupBuilder g)
    {
        g.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListRoomsQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.CentreStructureRead);
        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetRoomQuery(id), ct)))
            .RequireAuthorization(Permissions.CentreStructureRead);
        g.MapPost("/", async (CreateRoomCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapPatch("/{id:guid}", async (Guid id, UpdateRoomCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteRoomCommand(id), ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
    }

    private static void MapPrograms(RouteGroupBuilder g)
    {
        g.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListProgramsQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.CentreStructureRead);
        g.MapPost("/", async (CreateProgramCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapPatch("/{id:guid}", async (Guid id, UpdateProgramCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteProgramCommand(id), ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
    }

    private static void MapGroups(RouteGroupBuilder g)
    {
        g.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListGroupsQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.CentreStructureRead);
        g.MapPost("/", async (CreateGroupCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapPatch("/{id:guid}", async (Guid id, UpdateGroupCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteGroupCommand(id), ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
    }

    private static void MapLabels(RouteGroupBuilder g)
    {
        g.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListLabelsQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.CentreStructureRead);
        g.MapPost("/", async (CreateLabelCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapPatch("/{id:guid}", async (Guid id, UpdateLabelCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteLabelCommand(id), ct)))
            .RequireAuthorization(Permissions.CentreStructureWrite);
    }
}
