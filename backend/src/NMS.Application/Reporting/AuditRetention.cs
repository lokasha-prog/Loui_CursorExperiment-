using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Auditing;
using NMS.Domain.Common;
using NMS.Shared.Results;

namespace NMS.Application.Reporting;

public sealed class AuditOptions
{
    public const string Section = "Audit";

    /// <summary>How long history is kept when a centre has not chosen (2555 days is about seven years).</summary>
    public int DefaultRetentionDays { get; set; } = 2555;

    /// <summary>History younger than this can never be removed, by anyone. The database enforces the same floor.</summary>
    public int MinRetentionDays { get; set; } = 365;
}

public class AuditRetentionSetting : TenantEntity
{
    public int RetentionDays { get; set; }

    public bool AutoPurgeEnabled { get; set; }
}

public class AuditRetentionRun : TenantEntity
{
    public DateTimeOffset RunAt { get; set; }

    /// <summary>auto (scheduled) or manual (a super administrator pressed the button).</summary>
    public string Mode { get; set; } = "auto";

    public DateTimeOffset Cutoff { get; set; }

    public int RowsDeleted { get; set; }

    public Guid? ByUserId { get; set; }
}

public sealed record RetentionRunDto(DateTimeOffset RunAt, string Mode, DateTimeOffset Cutoff, int RowsDeleted);

public sealed record RetentionDto(
    int RetentionDays, bool AutoPurgeEnabled, int MinRetentionDays, long TotalEntries, DateTimeOffset? OldestEntryAt,
    long EligibleForPurge, DateTimeOffset Cutoff, IReadOnlyList<RetentionRunDto> RecentRuns);

public sealed record GetRetentionQuery : IRequest<Result<RetentionDto>>;

public sealed record SetRetentionCommand(int RetentionDays, bool AutoPurgeEnabled) : IRequest<Result<RetentionDto>>;

public sealed record PurgeAuditCommand(bool Confirm) : IRequest<Result<RetentionRunDto>>;

public sealed record ExportAuditQuery(DateOnly? From, DateOnly? To) : IRequest<Result<string>>;

/// <summary>Scheduled job, per centre: removes history older than the centre's policy, at most once a day.</summary>
public sealed record RunAuditRetentionCommand : IRequest<Result<int>>;

public sealed class SetRetentionValidator : AbstractValidator<SetRetentionCommand>
{
    public SetRetentionValidator(IOptions<AuditOptions> options) =>
        RuleFor(c => c.RetentionDays).InclusiveBetween(options.Value.MinRetentionDays, 36500)
            .WithMessage($"History must be kept for at least {options.Value.MinRetentionDays} days.");
}

internal static class Retention
{
    public static async Task<AuditRetentionSetting?> SettingAsync(IAppDbContext db, CancellationToken ct) =>
        await db.AuditRetentionSettings.FirstOrDefaultAsync(ct);

    public static int DaysFor(AuditRetentionSetting? setting, AuditOptions options) =>
        Math.Max(setting?.RetentionDays ?? options.DefaultRetentionDays, options.MinRetentionDays);

    public static async Task<RetentionRunDto> PurgeAsync(
        IAppDbContext db, ITenantContext tenant, ICurrentUser? current, IClock clock, int days, string mode, CancellationToken ct)
    {
        var cutoff = clock.UtcNow.AddDays(-days);
        var deleted = await db.PurgeAuditLogsAsync(cutoff, ct);
        db.AuditRetentionRuns.Add(new AuditRetentionRun
        {
            RunAt = clock.UtcNow, Mode = mode, Cutoff = cutoff, RowsDeleted = deleted, ByUserId = current is { IsAuthenticated: true } ? current.UserId : null,
        });

        // The purge is itself recorded in the history it just trimmed.
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.TenantId, UserId = current is { IsAuthenticated: true } ? current.UserId : null, UserEmail = current?.Email,
            EntityType = "audit_log", EntityId = Guid.Empty, Action = AuditActions.Delete, IpAddress = current?.IpAddress, OccurredAt = clock.UtcNow,
            ChangedFields = new() { ["rows_removed"] = new(null, deleted), ["older_than"] = new(null, cutoff.ToString("O")), ["mode"] = new(null, mode) },
        });
        await db.SaveChangesAsync(ct);
        return new RetentionRunDto(clock.UtcNow, mode, cutoff, deleted);
    }
}

public sealed class GetRetentionHandler(IAppDbContext db, IClock clock, IOptions<AuditOptions> options) : IRequestHandler<GetRetentionQuery, Result<RetentionDto>>
{
    public async Task<Result<RetentionDto>> Handle(GetRetentionQuery request, CancellationToken ct)
    {
        var setting = await Retention.SettingAsync(db, ct);
        var days = Retention.DaysFor(setting, options.Value);
        var cutoff = clock.UtcNow.AddDays(-days);
        var runs = await db.AuditRetentionRuns.AsNoTracking().OrderByDescending(r => r.RunAt).Take(10)
            .Select(r => new RetentionRunDto(r.RunAt, r.Mode, r.Cutoff, r.RowsDeleted)).ToListAsync(ct);
        return Result<RetentionDto>.Success(new RetentionDto(
            days, setting?.AutoPurgeEnabled ?? false, options.Value.MinRetentionDays,
            await db.AuditLogs.LongCountAsync(ct), await db.AuditLogs.MinAsync(l => (DateTimeOffset?)l.OccurredAt, ct),
            await db.AuditLogs.LongCountAsync(l => l.OccurredAt < cutoff, ct), cutoff, runs));
    }
}

public sealed class SetRetentionHandler(IAppDbContext db, IMediator mediator) : IRequestHandler<SetRetentionCommand, Result<RetentionDto>>
{
    public async Task<Result<RetentionDto>> Handle(SetRetentionCommand request, CancellationToken ct)
    {
        var setting = await Retention.SettingAsync(db, ct);
        if (setting is null)
        {
            setting = new AuditRetentionSetting();
            db.AuditRetentionSettings.Add(setting);
        }

        setting.RetentionDays = request.RetentionDays;
        setting.AutoPurgeEnabled = request.AutoPurgeEnabled;
        await db.SaveChangesAsync(ct);
        return await mediator.Send(new GetRetentionQuery(), ct);
    }
}

public sealed class PurgeAuditHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, IClock clock, IOptions<AuditOptions> options)
    : IRequestHandler<PurgeAuditCommand, Result<RetentionRunDto>>
{
    public async Task<Result<RetentionRunDto>> Handle(PurgeAuditCommand request, CancellationToken ct)
    {
        if (!request.Confirm)
        {
            return Error.Validation("Confirm that you want to permanently remove the old history.", "confirm");
        }

        var days = Retention.DaysFor(await Retention.SettingAsync(db, ct), options.Value);
        return Result<RetentionRunDto>.Success(await Retention.PurgeAsync(db, tenant, current, clock, days, "manual", ct));
    }
}

public sealed class RunAuditRetentionHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, IClock clock, IOptions<AuditOptions> options)
    : IRequestHandler<RunAuditRetentionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunAuditRetentionCommand request, CancellationToken ct)
    {
        var setting = await Retention.SettingAsync(db, ct);
        if (setting is not { AutoPurgeEnabled: true })
        {
            return Result<int>.Success(0);
        }

        var last = await db.AuditRetentionRuns.AsNoTracking().Where(r => r.Mode == "auto").MaxAsync(r => (DateTimeOffset?)r.RunAt, ct);
        if (last is not null && clock.UtcNow - last < TimeSpan.FromHours(23))
        {
            return Result<int>.Success(0);
        }

        var run = await Retention.PurgeAsync(db, tenant, current, clock, Retention.DaysFor(setting, options.Value), "auto", ct);
        return Result<int>.Success(run.RowsDeleted);
    }
}

/// <summary>Spreadsheet copy of the history (optionally for a date range), to keep before a purge. Cells that look like formulas are neutralised.</summary>
public sealed class ExportAuditHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, IClock clock) : IRequestHandler<ExportAuditQuery, Result<string>>
{
    public async Task<Result<string>> Handle(ExportAuditQuery request, CancellationToken ct)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (request.From is { } from) { var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); logs = logs.Where(l => l.OccurredAt >= start); }
        if (request.To is { } to) { var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); logs = logs.Where(l => l.OccurredAt < end); }

        var rows = await logs.OrderBy(l => l.OccurredAt).Take(200_000).ToListAsync(ct);
        var csv = new StringBuilder("When (UTC),User,Action,Record type,Record id,Changed fields,IP address\r\n");
        foreach (var l in rows)
        {
            csv.Append(Cell(l.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',').Append(Cell(l.UserEmail ?? "")).Append(',').Append(Cell(l.Action)).Append(',')
                .Append(Cell(l.EntityType)).Append(',').Append(Cell(l.EntityId.ToString())).Append(',')
                .Append(Cell(l.ChangedFields is null ? "" : string.Join("; ", l.ChangedFields.Keys))).Append(',').Append(Cell(l.IpAddress ?? "")).Append("\r\n");
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.TenantId, UserId = current.UserId, UserEmail = current.Email, EntityType = "audit_log", EntityId = Guid.Empty,
            Action = AuditActions.Export, IpAddress = current.IpAddress, OccurredAt = clock.UtcNow,
            ChangedFields = new() { ["rows"] = new(null, rows.Count) },
        });
        await db.SaveChangesAsync(ct);
        return Result<string>.Success(csv.ToString());
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
