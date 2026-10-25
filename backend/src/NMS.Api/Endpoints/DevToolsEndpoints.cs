using System.Net;
using MediatR;
using NMS.Api.Http;
using NMS.Application.Auth;
using NMS.Shared.Results;

namespace NMS.Api.Endpoints;

public sealed record QuickLoginRequest(string Role);

public sealed record DevOwnerLoginDto(string Name, string Email, string Password);

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

        group.MapGet("/owner-login", (HttpContext http, IConfiguration configuration) =>
        {
            if (!IsSameComputer(http))
            {
                return ApiResults.Error(http, StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Not found.");
            }

            var email = configuration["DevTools:OwnerEmail"];
            var password = configuration["DevTools:OwnerPassword"];
            var name = configuration["DevTools:OwnerName"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return ApiResults.Error(http, StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Not found.");
            }

            return ApiResults.From(http, Result<DevOwnerLoginDto>.Success(new DevOwnerLoginDto(
                string.IsNullOrWhiteSpace(name) ? "Salam" : name,
                email,
                password)));
        });

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
