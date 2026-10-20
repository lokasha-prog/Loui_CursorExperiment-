using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;

namespace NMS.Api.Jobs;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Set to false to switch every scheduled job off (for example while running a one-off tool).</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Runs the scheduled jobs for every active centre on a timer. Jobs are ordinary commands, so the same code is used by
/// tests and by the timer. A failure in one centre or one job is logged and never stops the others.
/// </summary>
public sealed class ScheduledJobsService(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<JobsOptions> options, ILogger<ScheduledJobsService> logger)
    : BackgroundService
{
    private static readonly IRequest<NMS.Shared.Results.Result<int>>[] Jobs = [new RunDailyCloseCommand(), new NMS.Application.Communication.RunDailyReportsCommand(), new NMS.Application.Notifications.RunNotificationDispatchCommand(), new NMS.Application.Reporting.RunAuditRetentionCommand()];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> centres;
        using (var scope = scopes.CreateScope())
        {
            centres = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Tenants.AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == "active").Select(t => t.Id).ToListAsync(ct);
        }

        foreach (var centre in centres)
        {
            foreach (var job in Jobs)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = centre;
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(job, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Scheduled job {Job} failed for a centre", job.GetType().Name);
                }
            }
        }
    }
}
