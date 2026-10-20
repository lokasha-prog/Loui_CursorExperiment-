using System.Net;
using MediatR;
using NMS.Api.Http;
using NMS.Application.Auth;

namespace NMS.Api.Endpoints;

public sealed record QuickLoginRequest(string Role);

/// <summary>Local test-user helper (ADR-0006 option A). Never mapped outside Development with DevTools:Enabled set.</summary>
public static class DevToolsEndpoints
{
    public const string SectionName = "DevTools";

    public static bool IsAvailable(IWebHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue<bool>($"{SectionName}:Enabled");

    public static void MapDevToolsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/dev").WithTags("Development helpers").AllowAnonymous();

        group.MapGet("/test-users", async (HttpContext http, ISender sender, CancellationToken ct) =>
            !IsSameComputer(http)
                ? ApiResults.Error(http, StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Not found.")
                : ApiResults.From(http, await sender.Send(new ListDevTestUsersQuery(), ct)));

        group.MapPost("/quick-login", async (QuickLoginRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!IsSameComputer(http))
            {
                return ApiResults.Error(http, StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Not found.");
            }

            var result = await sender.Send(new DevQuickLoginCommand(body.Role, http.Connection.RemoteIpAddress?.ToString()), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : AuthEndpoints.Respond(http, result.Value);
        }).RequireRateLimiting("login");
    }

    private static bool IsSameComputer(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip)
        && !http.Request.Headers.ContainsKey("X-Forwarded-For");
}
