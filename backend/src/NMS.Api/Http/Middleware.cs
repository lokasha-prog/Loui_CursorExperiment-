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
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Cache-Control"] = "no-store";

        // The API only returns data and files, never pages, so nothing on a response may load or run anything.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
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
