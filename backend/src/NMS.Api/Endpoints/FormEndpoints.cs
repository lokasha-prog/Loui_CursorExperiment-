using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Forms;
using NMS.Application.Security;
using NMS.Domain.Forms;

namespace NMS.Api.Endpoints;

public sealed record FormRequest(Dictionary<string, string> Name, Dictionary<string, string>? Description, string Type, bool IsPublic);

public sealed record FieldsRequest(List<FormField> Fields);

public sealed record ProgressRequest(string FieldId);

public sealed record SubmitRequest(Dictionary<string, JsonElement> Answers, Guid? StudentId, Guid? EnquiryId, Guid? StartId, string? CaptchaToken);

public static class FormEndpoints
{
    public static void MapFormEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/forms").WithTags("Forms");
        g.MapGet("/", async (string? type, string? status, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListFormsQuery(type, status, cursor, limit), ct))).RequireAuthorization(Permissions.FormsRead);
        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFormQuery(id), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/", async (FormRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CreateFormCommand(body.Name, body.Description, body.Type, body.IsPublic), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.FormsWrite);
        g.MapPatch("/{id:guid}", async (Guid id, FormRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new UpdateFormCommand(id, body.Name, body.Description, body.Type, body.IsPublic), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPut("/{id:guid}/fields", async (Guid id, FieldsRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveFieldsCommand(id, body.Fields), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/{id:guid}/actions/publish", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PublishFormCommand(id), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/{id:guid}/actions/archive", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetFormArchivedCommand(id, true), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/{id:guid}/actions/unarchive", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetFormArchivedCommand(id, false), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapGet("/{id:guid}/fill", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFillableFormQuery(id, null), ct))).RequireAuthorization(Permissions.FormsRead);
        g.MapPost("/{id:guid}/starts", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new StartFormCommand(id, null), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { start_id = result.Value }, StatusCodes.Status201Created);
        }).RequireAuthorization(Permissions.FormsRead);
        g.MapPost("/{id:guid}/starts/{startId:guid}/progress", async (Guid id, Guid startId, ProgressRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new FormProgressCommand(id, startId, body.FieldId, null), ct))).RequireAuthorization(Permissions.FormsRead);
        g.MapPost("/{id:guid}/submissions", async (Guid id, SubmitRequest body, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new SubmitFormCommand(id, body.Answers, body.StudentId, body.EnquiryId, body.StartId, null, null, null), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { id = result.Value }, StatusCodes.Status201Created);
        }).RequireAuthorization(Permissions.FormsRead);
        g.MapGet("/{id:guid}/submissions", async (Guid id, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListSubmissionsQuery(id, cursor, limit), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapGet("/{id:guid}/analytics", async (Guid id, DateOnly? from, DateOnly? to, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new FormAnalyticsQuery(id, from, to), ct))).RequireAuthorization(Permissions.FormsWrite);

        var pub = routes.MapGroup("/api/v1/public/forms").WithTags("Public").AllowAnonymous().RequireRateLimiting("anonymous");
        pub.MapGet("/{centre}/{id:guid}", async (string centre, Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFillableFormQuery(id, centre), ct)));
        pub.MapPost("/{centre}/{id:guid}/starts", async (string centre, Guid id, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new StartFormCommand(id, centre), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { start_id = result.Value }, StatusCodes.Status201Created);
        });
        pub.MapPost("/{centre}/{id:guid}/starts/{startId:guid}/progress", async (string centre, Guid id, Guid startId, ProgressRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new FormProgressCommand(id, startId, body.FieldId, centre), ct)));
        pub.MapPost("/{centre}/{id:guid}/submissions", async (string centre, Guid id, SubmitRequest body, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new SubmitFormCommand(id, body.Answers, body.StudentId, body.EnquiryId, body.StartId, centre, body.CaptchaToken, http.Connection.RemoteIpAddress?.ToString()), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { received = true }, StatusCodes.Status201Created);
        });
    }
}

public sealed record StepRequest(Dictionary<string, string> Name, Guid? FormId, int? DueDays);

public sealed record OrderRequest(IReadOnlyList<Guid> OrderedIds);

public static class AdmissionEndpoints
{
    public static void MapAdmissionEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/admissions").WithTags("Admissions");
        g.MapGet("/steps", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListStepsQuery(), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/steps", async (StepRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveStepCommand(null, body.Name, body.FormId, body.DueDays ?? 7), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.FormsWrite);
        g.MapPatch("/steps/{id:guid}", async (Guid id, StepRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SaveStepCommand(id, body.Name, body.FormId, body.DueDays ?? 7), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapDelete("/steps/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteStepCommand(id), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPut("/steps/order", async (OrderRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ReorderStepsCommand(body.OrderedIds), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapGet("/status", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AdmissionStatusQuery(), ct))).RequireAuthorization(Permissions.FormsWrite);
        g.MapPost("/students/{studentId:guid}/actions/start", async (Guid studentId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new StartAdmissionCommand(studentId), ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.FormsWrite);
        g.MapGet("/students/{studentId:guid}", async (Guid studentId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetStudentAdmissionQuery(studentId), ct))).RequireAuthorization(Permissions.FormsRead);
        g.MapPost("/students/{studentId:guid}/steps/{stepId:guid}/actions/{action}", async (Guid studentId, Guid stepId, string action, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new StepActionCommand(studentId, stepId, action), ct))).RequireAuthorization(Permissions.FormsWrite);
    }
}
