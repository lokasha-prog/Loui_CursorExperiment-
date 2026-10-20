using MediatR;
using NMS.Api.Http;
using NMS.Application.Security;
using NMS.Application.Users;

namespace NMS.Api.Endpoints;

public sealed record SetStatusRequest(string Status);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/v1/users").WithTags("Users");

        g.MapGet("/", async (string? cursor, int? limit, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.FromPage(http, await s.Send(new ListUsersQuery(cursor, limit), ct)))
            .RequireAuthorization(Permissions.UserManagementManage);

        g.MapPost("/", async (CreateUserCommand body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(body, ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.UserManagementManage);

        g.MapPost("/{id:guid}/actions/set-status", async (
            Guid id, SetStatusRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new SetUserStatusCommand(id, body.Status), ct)))
            .RequireAuthorization(Permissions.UserManagementManage);
    }
}
