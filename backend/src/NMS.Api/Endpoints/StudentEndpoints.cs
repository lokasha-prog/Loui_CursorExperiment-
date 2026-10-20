using MediatR;
using NMS.Api.Http;
using NMS.Application.Guardians;
using NMS.Application.Security;
using NMS.Application.Students;

namespace NMS.Api.Endpoints;

public sealed record TransitionStatusRequest(string Status, string? Reason);

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/students").WithTags("Students");

        g.MapGet("/", async (
            string? cursor, int? limit, string? status, Guid? roomId, string? search,
            HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListStudentsQuery(cursor, limit, status, roomId, search), ct)))
            .RequireAuthorization(Permissions.StudentsRead);

        g.MapPost("/", async (CreateStudentCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.StudentsWrite);

        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetStudentQuery(id), ct)))
            .RequireAuthorization(Permissions.StudentsRead);

        g.MapPatch("/{id:guid}", async (Guid id, UpdateStudentCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.StudentsWrite);

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteStudentCommand(id), ct)))
            .RequireAuthorization(Permissions.StudentsDelete);

        g.MapPost("/{id:guid}/actions/transition-status", async (
            Guid id, TransitionStatusRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new TransitionStudentStatusCommand(id, body.Status, body.Reason), ct)))
            .RequireAuthorization(Permissions.StudentsWrite);

        g.MapGet("/{id:guid}/guardians", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListGuardiansQuery(id), ct)))
            .RequireAuthorization(Permissions.StudentsRead);

        g.MapPost("/{id:guid}/guardians", async (Guid id, GuardianFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AddGuardianCommand(id, body), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.StudentsWrite);

        g.MapPatch("/{id:guid}/guardians/{guardianId:guid}", async (
            Guid id, Guid guardianId, GuardianFields body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new UpdateGuardianCommand(id, guardianId, body), ct)))
            .RequireAuthorization(Permissions.StudentsWrite);

        g.MapDelete("/{id:guid}/guardians/{guardianId:guid}", async (
            Guid id, Guid guardianId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new RemoveGuardianCommand(id, guardianId), ct)))
            .RequireAuthorization(Permissions.StudentsWrite);
    }
}
