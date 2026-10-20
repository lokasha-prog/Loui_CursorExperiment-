using System.Threading.Channels;
using MediatR;
using NMS.Application.Abstractions;
using NMS.Application.Reporting;

namespace NMS.Api.Jobs;

/// <summary>A simple in-process queue: requesting a report returns at once and this worker makes it in the background.</summary>
public sealed class ReportQueue : IReportQueue
{
    private readonly Channel<(Guid TenantId, Guid RunId)> _channel = Channel.CreateUnbounded<(Guid, Guid)>();

    public ChannelReader<(Guid TenantId, Guid RunId)> Reader => _channel.Reader;

    public void Enqueue(Guid tenantId, Guid runId) => _channel.Writer.TryWrite((tenantId, runId));
}

public sealed class ReportWorker(ReportQueue queue, IServiceScopeFactory scopes, ILogger<ReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (tenantId, runId) in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessReportRunCommand(runId), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Report run {RunId} failed in the worker.", runId);
            }
        }
    }
}
