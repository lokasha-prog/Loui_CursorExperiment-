using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Abstractions;
using NMS.Application.Ai;
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
        services.AddOptions<NMS.Application.Communication.DailyReportOptions>().Bind(configuration.GetSection(NMS.Application.Communication.DailyReportOptions.Section));
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<NMS.Application.Notifications.IEmailSender, SmtpEmailSender>();
        services.AddOptions<NMS.Infrastructure.Captcha.CaptchaOptions>().Bind(configuration.GetSection(NMS.Infrastructure.Captcha.CaptchaOptions.Section));
        services.AddHttpClient<NMS.Application.Enquiries.ICaptchaVerifier, NMS.Infrastructure.Captcha.RecaptchaVerifier>();
        services.AddSingleton<NMS.Application.Reporting.IEmergencyReportRenderer, QuestEmergencyReportRenderer>();
        services.AddOptions<NMS.Application.Reporting.AuditOptions>().Bind(configuration.GetSection(NMS.Application.Reporting.AuditOptions.Section))
            .Validate(o => o.MinRetentionDays >= 365 && o.DefaultRetentionDays >= o.MinRetentionDays, "Audit retention must keep at least 365 days and the default must not be lower than the minimum.")
            .ValidateOnStart();
        services.AddOptions<NMS.Application.Billing.BillingOptions>().Bind(configuration.GetSection(NMS.Application.Billing.BillingOptions.Section))
            .Validate(o => o.DefaultDueDays is >= 0 and <= 365, "Billing:DefaultDueDays must be between 0 and 365.")
            .ValidateOnStart();
        services.AddSingleton<NMS.Application.Billing.IInvoiceRenderer, QuestInvoiceRenderer>();
        services.AddSingleton<NMS.Application.Tools.IIdCardRenderer, NMS.Infrastructure.Services.QuestIdCardRenderer>();
        services.AddSingleton<CentreCalendar>();

        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName));
        services.AddHttpClient<IAiBrain, GroqAiBrain>((provider, client) =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(ai.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });

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
