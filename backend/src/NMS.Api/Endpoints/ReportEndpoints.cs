using MediatR;
using NMS.Api.Http;
using NMS.Application.Reporting;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record ReportRequest(string Kind, string Format, Dictionary<string, string?>? Parameters, string? Language);

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/reports/analytics", async (DateOnly from, DateOnly to, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AnalyticsQuery(from, to), ct))).WithTags("Reports").RequireAuthorization(Permissions.ReportsView);

        var g = routes.MapGroup("/api/v1/reports/center").WithTags("Reports");
        g.MapGet("/definitions", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListReportDefinitionsQuery(), ct))).RequireAuthorization(Permissions.ReportsView);
        g.MapPost("/runs", async (ReportRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new RequestReportCommand(body.Kind, body.Format, body.Parameters, body.Language), ct), StatusCodes.Status202Accepted)).RequireAuthorization(Permissions.ReportsView);
        g.MapGet("/runs", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListReportRunsQuery(cursor, limit), ct))).RequireAuthorization(Permissions.ReportsView);
        g.MapGet("/runs/{id:guid}/file", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new DownloadReportQuery(id), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).RequireAuthorization(Permissions.ReportsView);
    }
}
