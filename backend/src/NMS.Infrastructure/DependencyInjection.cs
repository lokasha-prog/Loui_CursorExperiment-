using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Auth;
using NMS.Infrastructure.Persistence;
using NMS.Infrastructure.Persistence.Interceptors;
using NMS.Infrastructure.Security;
using NMS.Infrastructure.Services;

namespace NMS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the connection string that points at the local SQL Server database.</summary>
    public const string ConnectionStringName = "Nms";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => TryKeyLength(o.SigningKey) >= 32, "Jwt:SigningKey must be base64 text of at least 32 bytes.")
            .ValidateOnStart();
        services.AddOptions<EncryptionOptions>().Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => TryKeyLength(o.Key) == 32, "Encryption:Key must be base64 text of exactly 32 bytes.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(
                o => o.MaxFailedAttempts > 0 && o.LockoutMinutes > 0 && o.AccessTokenMinutes > 0
                    && o.RefreshTokenDays > 0 && o.MinPasswordLength > 0,
                "The Auth section must define MaxFailedAttempts, LockoutMinutes, AccessTokenMinutes, RefreshTokenDays and MinPasswordLength.")
            .ValidateOnStart();

        services.AddOptions<AttendanceOptions>().Bind(configuration.GetSection(AttendanceOptions.Section))
            .Validate(
                o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZoneId, out _) && o.LateGraceMinutes >= 0 && o.EarlyLeaveGraceMinutes >= 0,
                "The Attendance section needs a valid TimeZoneId and non-negative grace minutes.")
            .ValidateOnStart();
        services.AddSingleton<CentreCalendar>();

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton<TenantSessionInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<NmsDbContext>((provider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .AddInterceptors(
                provider.GetRequiredService<TenantSessionInterceptor>(),
                provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<NmsDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IAccessScope, AccessScope>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        return services;
    }

    private static int TryKeyLength(string value)
    {
        try
        {
            return Convert.FromBase64String(value).Length;
        }
        catch (FormatException)
        {
            return -1;
        }
    }
}
