using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Abstractions;
using NMS.Application.CentreGroups;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record ProvisionCentreRequest(string Name, string? NameArabic, string Slug);

public sealed record AssignStaffRequest(string Email, Guid CentreId, string Role);

public sealed record TransferStudentRequest(Guid ToCentreId, string? Reason);

public sealed record SwitchCentreRequest(Guid CentreId);

public static class CentreGroupEndpoints
{
    public static void MapCentreGroupEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/group").WithTags("Centre groups");

        // Any signed-in person may list and switch between the centres of their group where they already have an account.
        g.MapGet("/my-centres", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new MyCentresQuery(), ct))).RequireAuthorization();
        g.MapPost("/switch", async (SwitchCentreRequest body, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new SwitchCentreCommand(body.CentreId, http.Connection.RemoteIpAddress?.ToString()), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : AuthEndpoints.Respond(http, result.Value);
        }).RequireAuthorization();

        g.MapGet("/overview", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GroupOverviewQuery(), ct))).RequireAuthorization(Permissions.GroupRead);
        g.MapGet("/reports/{kind}", async (
            string kind, string? format, string? lang, HttpContext http, ISender s, NMS.Application.Reporting.IReportRenderer renderer, CancellationToken ct) =>
        {
            var result = await s.Send(new GroupReportQuery(kind, lang == "ar" ? "ar" : "en"), ct);
            if (result.IsFailure)
            {
                return ApiResults.Failure(http, result.Errors);
            }

            if (string.IsNullOrEmpty(format) || format == "json")
            {
                var t = result.Value;
                return ApiResults.Ok(http, new { title = t.Title, subtitle = t.Subtitle, columns = t.Columns, rows = t.Rows, summary = t.Summary.Select(x => new { label = x.Label, value = x.Value }) });
            }

            if (format is not ("excel" or "pdf"))
            {
                return ApiResults.Error(http, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Format must be json, excel or pdf.");
            }

            var file = renderer.Render(result.Value, format);
            return Results.File(file.Content, file.ContentType, $"group-{kind}.{file.Extension}");
        }).RequireAuthorization(Permissions.GroupRead);

        g.MapPost("/centres", async (ProvisionCentreRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ProvisionCentreCommand(body.Name, body.NameArabic, body.Slug), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.GroupManage);
        g.MapPost("/staff-assignments", async (AssignStaffRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AssignStaffToCentreCommand(body.Email, body.CentreId, body.Role), ct)))
            .RequireAuthorization(Permissions.GroupManage);
        g.MapPost("/students/{id:guid}/transfer", async (Guid id, TransferStudentRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new TransferStudentCommand(id, body.ToCentreId, body.Reason), ct)))
            .RequireAuthorization(Permissions.GroupManage);
    }
}
