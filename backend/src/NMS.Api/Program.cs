using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NMS.Api.Endpoints;
using NMS.Api.Http;
using NMS.Application;
using NMS.Application.Abstractions;
using NMS.Application.Security;
using NMS.Infrastructure;
using NMS.Infrastructure.Persistence;
using NMS.Infrastructure.Security;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .MinimumLevel.Is(context.Configuration.GetValue("Logging:Serilog:MinimumLevel", Serilog.Events.LogEventLevel.Information))
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .Validate(o => o.AuthenticatedPerMinute > 0 && o.AnonymousPerMinute > 0 && o.LoginPerMinute > 0 && o.SessionPerMinute > 0,
        "The RateLimiting section must define AuthenticatedPerMinute, AnonymousPerMinute, LoginPerMinute and SessionPerMinute.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = jwt.Value.CreateKey(),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in RolePermissions.AllPermissionNames())
    {
        options.AddPolicy(permission, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                RolePermissions.ForRoles(context.User.FindAll("role").Select(c => c.Value)).Contains(permission)));
    }

    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await ApiResults.Error(context.HttpContext, StatusCodes.Status429TooManyRequests, "RATE_LIMIT_EXCEEDED", "Too many requests.")
            .ExecuteAsync(context.HttpContext);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var limits = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var userId = http.User.FindFirst("sub")?.Value;

        // Page loads call these two before anyone is signed in; they have their own, more generous limit below.
        if (http.Request.Path.StartsWithSegments("/api/v1/auth/refresh") || http.Request.Path.StartsWithSegments("/api/v1/auth/setup-status"))
        {
            return Window($"session:{http.Connection.RemoteIpAddress}", limits.SessionPerMinute);
        }

        return userId is not null
            ? Window($"user:{userId}", limits.AuthenticatedPerMinute)
            : Window($"ip:{http.Connection.RemoteIpAddress}", limits.AnonymousPerMinute);
    });
    options.AddPolicy("anonymous", http => Window(
        $"anon:{http.Connection.RemoteIpAddress}",
        http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.AnonymousPerMinute));
    options.AddPolicy("login", http => Window(
        $"login:{http.Connection.RemoteIpAddress}",
        http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.LoginPerMinute));
});

var app = builder.Build();

ExceptionHandling.Use(app);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NmsDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (NmsDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapCentreStructureEndpoints();
app.MapStudentEndpoints();
app.MapStaffEndpoints();
app.MapAttendanceEndpoints();
app.MapUserEndpoints();

app.Run();

static RateLimitPartition<string> Window(string key, int permitsPerMinute) =>
    RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    });

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthenticatedPerMinute { get; set; }

    public int AnonymousPerMinute { get; set; }

    public int LoginPerMinute { get; set; }

    /// <summary>Limit for session refresh and first-run status checks, which every page load makes.</summary>
    public int SessionPerMinute { get; set; }
}

public partial class Program;
