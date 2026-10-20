using MediatR;
using Microsoft.AspNetCore.Mvc;
using NMS.Api.Http;
using NMS.Application.Billing;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record AssignStudentsRequest(IReadOnlyList<Guid> StudentIds, DateOnly EffectiveFrom);

public sealed record EndStudentPlanRequest(DateOnly EffectiveTo);

public sealed record RecordPaymentRequest(long AmountFils, DateOnly PaymentDate, string Method, string? Reference, string? Notes);

public sealed record GenerateRequest(string? Month);

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder routes)
    {
        var plans = routes.MapGroup("/api/v1/fee-plans").WithTags("Billing");
        plans.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListFeePlansQuery(cursor, limit), ct))).RequireAuthorization(Permissions.BillingWrite);
        plans.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetFeePlanQuery(id), ct))).RequireAuthorization(Permissions.BillingWrite);
        plans.MapPost("/", async (CreateFeePlanCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.BillingWrite);
        plans.MapPatch("/{id:guid}", async (Guid id, UpdateFeePlanCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body with { Id = id }, ct))).RequireAuthorization(Permissions.BillingWrite);
        plans.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteFeePlanCommand(id), ct))).RequireAuthorization(Permissions.BillingWrite);
        plans.MapPost("/{id:guid}/students", async (Guid id, AssignStudentsRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new AssignStudentsCommand(id, body.StudentIds, body.EffectiveFrom), ct))).RequireAuthorization(Permissions.BillingWrite);
        plans.MapPost("/{id:guid}/students/{studentId:guid}/actions/end", async (Guid id, Guid studentId, EndStudentPlanRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new EndStudentPlanCommand(id, studentId, body.EffectiveTo), ct))).RequireAuthorization(Permissions.BillingWrite);

        var invoices = routes.MapGroup("/api/v1/invoices").WithTags("Billing");
        invoices.MapGet("/", async (string? status, [FromQuery(Name = "student_id")] Guid? studentId, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListInvoicesQuery(status, studentId, cursor, limit), ct))).RequireAuthorization(Permissions.BillingRead);
        invoices.MapGet("/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetInvoiceQuery(id), ct))).RequireAuthorization(Permissions.BillingRead);
        invoices.MapGet("/{id:guid}/pdf", async (Guid id, string? language, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new InvoicePdfQuery(id, language ?? "en"), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.File(result.Value.Pdf, "application/pdf", result.Value.FileName);
        }).RequireAuthorization(Permissions.BillingRead);
        invoices.MapPost("/", async (CreateInvoiceCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created)).RequireAuthorization(Permissions.BillingWrite);
        invoices.MapPost("/{id:guid}/actions/issue", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new IssueInvoiceCommand(id), ct))).RequireAuthorization(Permissions.BillingWrite);
        invoices.MapPost("/{id:guid}/actions/cancel", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new CancelInvoiceCommand(id), ct))).RequireAuthorization(Permissions.BillingWrite);
        invoices.MapPost("/{id:guid}/actions/send", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SendInvoiceCommand(id), ct))).RequireAuthorization(Permissions.BillingWrite);
        invoices.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new RecordPaymentCommand(id, body.AmountFils, body.PaymentDate, body.Method, body.Reference, body.Notes), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.PaymentsRecord);
        invoices.MapPost("/actions/generate", async (GenerateRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new GenerateInvoicesCommand(body?.Month), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { created = result.Value });
        }).RequireAuthorization(Permissions.InvoicesGenerate);

        routes.MapGet("/api/v1/dashboard/billing-snapshot", async (string? month, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new BillingSnapshotQuery(month), ct))).WithTags("Dashboard").RequireAuthorization(Permissions.BillingWrite);
    }
}
