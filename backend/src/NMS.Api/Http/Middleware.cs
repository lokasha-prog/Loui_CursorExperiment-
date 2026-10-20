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
