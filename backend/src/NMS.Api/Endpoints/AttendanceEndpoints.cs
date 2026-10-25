using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Ai;
using NMS.Application.Attendance;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record DecideLeaveRequest(string? Notes);

public sealed record EditAttendanceRequest(string Status, string? Notes);

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

        a.MapGet("/report/monthly", async (string month, [FromQuery(Name = "person_type")] string? personType, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new MonthlyReportQuery(month, personType ?? "student"), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);
        a.MapGet("/export", async (string month, [FromQuery(Name = "person_type")] string? personType, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new ExportAttendanceQuery(month, personType ?? "student"), ct);
            return result.IsFailure
                ? ApiResults.Failure(http, result.Errors)
                : Results.File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(result.Value)).ToArray(),
                    "text/csv; charset=utf-8", $"attendance-{month}.csv");
        }).RequireAuthorization(Permissions.AttendanceOverride);
        a.MapGet("/forecast", async (DateOnly? date, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ForecastQuery(date), ct)))
            .RequireAuthorization(Permissions.AttendanceWrite);
        a.MapPatch("/{id:guid}", async (Guid id, EditAttendanceRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new EditAttendanceCommand(id, body.Status, body.Notes), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);
        a.MapGet("/ratio/history", async (
            [FromQuery(Name = "room_id")] Guid? roomId, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new RatioHistoryQuery(roomId, cursor, limit), ct)))
            .RequireAuthorization(Permissions.AttendanceOverride);

        routes.MapGet("/api/v1/dashboard/attendance-summary", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DashboardSummaryQuery(), ct)))
            .WithTags("Dashboard")
            .RequireAuthorization(Permissions.DashboardView);

        routes.MapGet("/api/v1/ai/attendance-summary", async (int? days, string? language, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AttendanceInsightsQuery(days ?? 14, language ?? "en"), ct)))
            .WithTags("AI Brain")
            .RequireAuthorization(Permissions.AttendanceOverride);

        routes.MapGet("/api/v1/dashboard/system", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SystemOverviewQuery(), ct)))
            .WithTags("Dashboard")
            .RequireAuthorization(Permissions.DashboardView);

        routes.MapGet("/api/v1/system/queues", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new QueueHealthQuery(), ct)))
            .WithTags("Dashboard")
            .RequireAuthorization(Permissions.SettingsWrite);

        routes.MapPost("/api/v1/ai/chat", async (DataChatCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct)))
            .WithTags("AI Brain")
            .RequireAuthorization(Permissions.AiChat);

        // AI Brain briefings for management: each area needs the permission for the data it summarises.
        foreach (var (area, permission) in new[]
        {
            (BriefingAreas.Overview, Permissions.AttendanceOverride), (BriefingAreas.Finance, Permissions.BillingRead),
            (BriefingAreas.Admissions, Permissions.EnquiriesRead), (BriefingAreas.People, Permissions.StaffRead),
        })
        {
            var captured = area;
            routes.MapGet($"/api/v1/ai/briefing/{area}", async (string? language, HttpContext http, ISender s, CancellationToken ct) =>
                ApiResults.From(http, await s.Send(new ManagementBriefingQuery(captured, language ?? "en"), ct)))
                .WithTags("AI Brain")
                .RequireAuthorization(permission);
        }

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
