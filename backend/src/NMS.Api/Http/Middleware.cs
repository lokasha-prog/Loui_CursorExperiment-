using Microsoft.AspNetCore.Diagnostics;
using NMS.Application.Abstractions;

namespace NMS.Api.Http;

/// <summary>Copies the tenant from the validated token into the request's tenant context (spec 10 §4.2).</summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tenantId))
            {
                await ApiResults.Error(context, StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Invalid session.")
                    .ExecuteAsync(context);
                return;
            }

            tenant.TenantId = tenantId;
        }

        await next(context);
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string WebAppPolicy =
        "default-src 'self'; script-src 'self' https://www.google.com/recaptcha/ https://www.gstatic.com/recaptcha/; "
        + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' data: https://fonts.gstatic.com; "
        + "img-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self' ws: wss:; frame-src https://www.google.com/recaptcha/; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Cache-Control"] = "no-store";

        // Data and file answers (api, live updates, health) are never pages, so nothing on them may load or run anything.
        // The web app itself may use its own files plus the two outside services it needs: Google Fonts and the captcha on the public enquiry form.
        var path = context.Request.Path;
        var isData = path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs") || path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi");
        headers["Content-Security-Policy"] = isData ? "default-src 'none'; frame-ancestors 'none'" : WebAppPolicy;
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        return next(context);
    }
}

public static class ExceptionHandling
{
    /// <summary>Logs unexpected failures and returns a generic 500 envelope without internal details.</summary>
    public static void Use(IApplicationBuilder app) =>
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();

            // A missing or badly written query value (for example a date that is not a date) is the caller's mistake, not ours.
            if (feature?.Error is BadHttpRequestException)
            {
                await ApiResults.Error(context, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "The request is not valid. Check the values you sent.").ExecuteAsync(context);
                return;
            }

            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Unhandled");
            if (feature is not null)
            {
                logger.LogError(feature.Error, "Unhandled exception. RequestId: {RequestId}", context.TraceIdentifier);
            }

            await ApiResults.Error(
                context,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred.").ExecuteAsync(context);
        }));
}
