using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Attendance;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record DecideLeaveRequest(string? Notes);

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder routes)
    {
        var a = routes.MapGroup("/api/v1/attendance").WithTags("Attendance");

        a.MapGet("/", async (
            DateOnly? date, [FromQuery(Name = "room_id")] Guid? roomId, [FromQuery(Name = "person_type")] string? personType, string? cursor, int? limit,
            HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListAttendanceQuery(date, roomId, personType, cursor, limit), ct)))
            .RequireAuthorization(Permissions.AttendanceRead);
        a.MapPost("/actions/check-in", async (CheckInCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);
        a.MapPost("/actions/check-out", async (CheckOutCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);
        a.MapPost("/actions/mark-absent", async (MarkAbsentCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);
        a.MapGet("/ratio/live", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new LiveRatioQuery(), ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);

        routes.MapGet("/api/v1/dashboard/attendance-summary", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DashboardSummaryQuery(), ct)))
            .WithTags("Dashboard")
            .RequireAuthorization(Permissions.DashboardView);

        var types = routes.MapGroup("/api/v1/leave-types").WithTags("Leaves");
        types.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListLeaveTypesQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.AttendanceRead);
        types.MapPost("/", async (CreateLeaveTypeCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.AttendanceOverride);
        types.MapPatch("/{id:guid}", async (Guid id, UpdateLeaveTypeCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);
        types.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteLeaveTypeCommand(id), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);

        var leaves = routes.MapGroup("/api/v1/leaves").WithTags("Leaves");
        leaves.MapGet("/", async (
            string? status, [FromQuery(Name = "person_type")] string? personType, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListLeavesQuery(status, personType, cursor, limit), ct)))
            .RequireAuthorization(Permissions.AttendanceRead);
        leaves.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetLeaveQuery(id), ct)))
            .RequireAuthorization(Permissions.AttendanceRead);
        leaves.MapPost("/", async (CreateLeaveCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.AttendanceWrite);
        leaves.MapPost("/{id:guid}/actions/approve", async (Guid id, DecideLeaveRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DecideLeaveCommand(id, true, body?.Notes), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);
        leaves.MapPost("/{id:guid}/actions/reject", async (Guid id, DecideLeaveRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DecideLeaveCommand(id, false, body?.Notes), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);
        leaves.MapPost("/{id:guid}/actions/cancel", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CancelLeaveCommand(id), ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);
    }
}
