using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Api.Http;
using NMS.Application.Abstractions;
using NMS.Application.Auth;

namespace NMS.Api.Endpoints;

public sealed record LoginRequest(string? Tenant, string Email, string Password);

public sealed record BootstrapRequest(
    string BootstrapToken,
    string TenantSlug,
    Dictionary<string, string> TenantName,
    string AdminEmail,
    string AdminFirstName,
    string AdminLastName,
    string AdminPassword);

public sealed record SessionResponse(
    string AccessToken, DateTimeOffset ExpiresAt, MeDto Me,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken = null);

public sealed record MobileRefreshRequest(string? RefreshToken);

public static class AuthEndpoints
{
    public const string RefreshCookie = "nms_refresh";
    private const string CsrfHeader = "X-Requested-With";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/auth").WithTags("Auth");

        // Public: tells the sign-in screens whether first-time setup is needed and which password rule applies (from configuration).
        group.MapGet("/setup-status", async (
            HttpContext http, IAppDbContext db, IOptions<AuthOptions> auth, CancellationToken ct) =>
            ApiResults.Ok(http, new
            {
                setup_required = !await db.Tenants.AnyAsync(ct),
                password_min_length = auth.Value.MinPasswordLength,
            }))
            .AllowAnonymous();

        group.MapPost("/bootstrap-super-admin", async (BootstrapRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new BootstrapCommand(
                body.BootstrapToken, body.TenantSlug, body.TenantName, body.AdminEmail,
                body.AdminFirstName, body.AdminLastName, body.AdminPassword), ct);
            return ApiResults.From(http, result, StatusCodes.Status201Created);
        }).AllowAnonymous().RequireRateLimiting("anonymous");

        group.MapPost("/login", async (LoginRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(
                body.Tenant, body.Email, body.Password, http.Connection.RemoteIpAddress?.ToString()), ct);
            if (result.IsFailure)
            {
                return ApiResults.Failure(http, result.Errors);
            }

            return result.Value.Session is { } session
                ? Respond(http, session)
                : ApiResults.Ok(http, new { choose_centre = result.Value.Centres });
        }).AllowAnonymous().RequireRateLimiting("login");

        group.MapPost("/refresh", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(CsrfHeader))
            {
                return ApiResults.Error(http, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", $"The {CsrfHeader} header is required.");
            }

            var token = http.Request.Cookies[RefreshCookie];
            if (string.IsNullOrEmpty(token) && IsMobile(http))
            {
                token = (await http.Request.ReadFromJsonAsync<MobileRefreshRequest>(ct))?.RefreshToken;
            }

            if (string.IsNullOrEmpty(token))
            {
                return ApiResults.Error(http, StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Session expired.");
            }

            var result = await sender.Send(new RefreshSessionCommand(token, http.Connection.RemoteIpAddress?.ToString()), ct);
            if (result.IsFailure)
            {
                http.Response.Cookies.Delete(RefreshCookie, CookieOptions(http, null));
                return ApiResults.Failure(http, result.Errors);
            }

            return Respond(http, result.Value);
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(CsrfHeader))
            {
                return ApiResults.Error(http, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", $"The {CsrfHeader} header is required.");
            }

            var token = http.Request.Cookies[RefreshCookie];
            if (string.IsNullOrEmpty(token) && IsMobile(http))
            {
                token = (await http.Request.ReadFromJsonAsync<MobileRefreshRequest>(ct))?.RefreshToken;
            }

            await sender.Send(new LogoutCommand(token), ct);
            http.Response.Cookies.Delete(RefreshCookie, CookieOptions(http, null));
            return Results.NoContent();
        }).AllowAnonymous();

        group.MapGet("/me", async (HttpContext http, ISender sender, CancellationToken ct) =>
            ApiResults.From(http, await sender.Send(new GetMeQuery(), ct)))
            .RequireAuthorization();
    }

    /// <summary>The refresh token travels only in an HttpOnly cookie; the body carries just the short-lived access token.</summary>
    internal static IResult Respond(HttpContext http, AuthSession session)
    {
        http.Response.Cookies.Append(RefreshCookie, session.RefreshToken, CookieOptions(http, session.RefreshTokenExpiresAt));

        // The phone app cannot read an HttpOnly cookie, so it is also given the refresh token to keep in the phone's secure store.
        return ApiResults.Ok(http, new SessionResponse(session.AccessToken, session.AccessTokenExpiresAt, session.Me, IsMobile(http) ? session.RefreshToken : null));
    }

    internal static bool IsMobile(HttpContext http) => string.Equals(http.Request.Headers["X-NMS-Client"], "mobile", StringComparison.OrdinalIgnoreCase);

    private static CookieOptions CookieOptions(HttpContext http, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires,
    };
}
