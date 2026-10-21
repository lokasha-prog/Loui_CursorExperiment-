using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Common;

namespace NMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<ShortCache>();
        services.AddOptions<CachingOptions>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ShortCacheBehavior<,>));
        services.AddScoped<NMS.Application.Notifications.Notifier>();
        services.AddScoped<NMS.Application.CentreGroups.CentreRunner>();
        services.AddScoped<NMS.Application.Billing.InvoiceGenerator>();
        services.AddScoped<NMS.Application.Billing.BillingSettingsReader>();
        services.AddScoped<NMS.Application.Settings.CentreSettingsReader>();
        services.AddScoped<NMS.Application.Calendar.EventExpander>();
        services.AddScoped<NMS.Application.Forms.AdmissionService>();
        services.AddScoped<NMS.Application.Reporting.ReportBuilder>();
        services.AddScoped<NMS.Application.Newsletters.NewsletterSender>();
        services.AddScoped<NMS.Application.Calendar.HolidayChecker>();
        services.AddScoped<NMS.Application.Enquiries.TourMail>();
        return services;
    }
}
