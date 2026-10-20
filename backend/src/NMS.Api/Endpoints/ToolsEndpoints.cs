using MediatR;
using NMS.Api.Http;
using NMS.Application.Security;
using NMS.Application.Tools;

namespace NMS.Api.Endpoints;

public static class ToolsEndpoints
{
    public static void MapToolsEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/tools").WithTags("Tools");
        g.MapGet("/id-card-templates", async (string? scope, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new GetIdCardTemplatesQuery(scope), ct))).RequireAuthorization(Permissions.ToolsUse);
        g.MapPost("/id-cards", async (IdCardRequest body, HttpContext http, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(new GenerateIdCardsQuery(body), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : Results.File(result.Value.Pdf, "application/pdf", result.Value.FileName);
        }).RequireAuthorization(Permissions.ToolsUse);
    }
}
