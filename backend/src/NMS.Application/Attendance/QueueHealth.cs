using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Forms;
using NMS.Domain.Notifications;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

/// <summary>The numbers behind the "should the alert sender or the media handler become separate services" decision gates (spec 19, Sprint 25).</summary>
public sealed record QueueHealthDto(
    int AlertsWaiting, int OldestAlertWaitingMinutes, int AlertsFailedLast24Hours, int AlertsSentLast24Hours, int ReportsWaiting, int ReportsFailedLast24Hours, int MediaFiles, long MediaMegabytes);

public sealed record QueueHealthQuery : IRequest<Result<QueueHealthDto>>;

public sealed class QueueHealthHandler(IAppDbContext db, IClock clock) : IRequestHandler<QueueHealthQuery, Result<QueueHealthDto>>
{
    public async Task<Result<QueueHealthDto>> Handle(QueueHealthQuery request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var day = now.AddDays(-1);
        var waiting = db.NotificationDeliveries.AsNoTracking().Where(d => d.Status == DeliveryStatuses.Pending && d.Channel != NotificationChannels.InApp);
        var count = await waiting.CountAsync(ct);
        var oldest = count == 0 ? (DateTimeOffset?)null : await waiting.MinAsync(d => (DateTimeOffset?)d.NextAttemptAt, ct);
        var failed = await db.NotificationDeliveries.AsNoTracking().CountAsync(d => d.Status == DeliveryStatuses.Failed && d.UpdatedAt >= day, ct);
        var sent = await db.NotificationDeliveries.AsNoTracking().CountAsync(d => d.Status == DeliveryStatuses.Sent && d.Channel != NotificationChannels.InApp && d.SentAt >= day, ct);
        var reports = await db.ReportRuns.AsNoTracking().CountAsync(r => r.Status == ReportStatuses.Pending || r.Status == ReportStatuses.Running, ct);
        var reportsFailed = await db.ReportRuns.AsNoTracking().CountAsync(r => r.Status == ReportStatuses.Failed && r.UpdatedAt >= day, ct);
        var media = await db.MediaFiles.AsNoTracking().CountAsync(ct);
        var bytes = await db.MediaFiles.AsNoTracking().SumAsync(m => (long?)m.SizeBytes, ct) ?? 0;
        return Result<QueueHealthDto>.Success(new QueueHealthDto(count, oldest is null ? 0 : (int)Math.Max(0, (now - oldest.Value).TotalMinutes), failed, sent, reports, reportsFailed, media, bytes / 1_048_576));
    }
}
