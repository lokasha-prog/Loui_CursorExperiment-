using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Notifications;
using NMS.Application.Reporting;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record PreferencesRequest(IReadOnlyList<PreferenceDto> Preferences);

public sealed record RetentionRequest(int RetentionDays, bool AutoPurgeEnabled);

public sealed record PurgeRequest(bool Confirm);

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var n = routes.MapGroup("/api/v1/notifications").WithTags("Notifications");
        n.MapGet("/", async ([FromQuery(Name = "unread_only")] bool? unreadOnly, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListNotificationsQuery(unreadOnly ?? false, cursor, limit), ct)))
            .RequireAuthorization(Permissions.DashboardView);
        n.MapGet("/unread-count", async (HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new UnreadCountQuery(), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { count = result.Value });
        }).RequireAuthorization(Permissions.DashboardView);
        n.MapPost("/{id:guid}/actions/read", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new MarkReadCommand(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { marked = result.Value });
        }).RequireAuthorization(Permissions.DashboardView);
        n.MapPost("/actions/read-all", async (HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new MarkReadCommand(null), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { marked = result.Value });
        }).RequireAuthorization(Permissions.DashboardView);
        n.MapGet("/preferences", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetPreferencesQuery(), ct)))
            .RequireAuthorization(Permissions.NotificationsPreferences);
        n.MapPut("/preferences", async (PreferencesRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetPreferencesCommand(body.Preferences), ct)))
            .RequireAuthorization(Permissions.NotificationsPreferences);

        routes.MapGet("/api/v1/audit-logs", async (
            [FromQuery(Name = "user_id")] Guid? userId, [FromQuery(Name = "entity_type")] string? entityType, string? action,
            DateOnly? from, DateOnly? to, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListAuditLogsQuery(userId, entityType, action, from, to, cursor, limit), ct)))
            .WithTags("Audit").RequireAuthorization(Permissions.AuditLogView);

        var audit = routes.MapGroup("/api/v1/audit-logs").WithTags("Audit");
        audit.MapGet("/retention", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetRetentionQuery(), ct))).RequireAuthorization(Permissions.AuditLogView);
        audit.MapPut("/retention", async (RetentionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetRetentionCommand(body.RetentionDays, body.AutoPurgeEnabled), ct))).RequireAuthorization(Permissions.AuditLogView);
        audit.MapPost("/retention/actions/purge", async (PurgeRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PurgeAuditCommand(body.Confirm), ct))).RequireAuthorization(Permissions.AuditLogView);
        audit.MapGet("/export", async (DateOnly? from, DateOnly? to, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new ExportAuditQuery(from, to), ct);
            return result.IsFailure
                ? ApiResults.Failure(http, result.Errors)
                : Results.File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(result.Value)).ToArray(),
                    "text/csv; charset=utf-8", $"audit-log-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).RequireAuthorization(Permissions.AuditLogView);

        routes.MapGet("/api/v1/students/{id:guid}/emergency-report", async (Guid id, string? language, HttpContext http, ISender s, CancellationToken ct) =>
            Pdf(http, await s.Send(new EmergencyReportQuery(id, null, language ?? "en"), ct)))
            .WithTags("Emergency").RequireAuthorization(Permissions.AttendanceWrite);
        routes.MapGet("/api/v1/rooms/{id:guid}/emergency-report", async (Guid id, string? language, HttpContext http, ISender s, CancellationToken ct) =>
            Pdf(http, await s.Send(new EmergencyReportQuery(null, id, language ?? "en"), ct)))
            .WithTags("Emergency").RequireAuthorization(Permissions.AttendanceWrite);
    }

    private static IResult Pdf(HttpContext http, NMS.Shared.Results.Result<EmergencyReportFile> result) =>
        result.IsFailure
            ? ApiResults.Failure(http, result.Errors)
            : Results.File(result.Value.Pdf, "application/pdf", result.Value.FileName);
}
