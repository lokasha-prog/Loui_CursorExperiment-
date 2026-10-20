using MediatR;
using NMS.Api.Http;
using NMS.Application.Feedback;
using NMS.Application.Security;

namespace NMS.Api.Endpoints;

public sealed record SetFeedbackStatusRequest(string Status);

public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/feedback").WithTags("Feedback");
        g.MapPost("/", async (SendFeedbackCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.DashboardView);
        g.MapGet("/", async (string? status, string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListFeedbackQuery(status, cursor, limit), ct)))
            .RequireAuthorization(Permissions.SettingsRead);
        g.MapPatch("/{id:guid}", async (Guid id, SetFeedbackStatusRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetFeedbackStatusCommand(id, body.Status), ct)))
            .RequireAuthorization(Permissions.SettingsWrite);
    }
}
