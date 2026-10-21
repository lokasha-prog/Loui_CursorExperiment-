using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Communication;
using NMS.Application.Security;
using NMS.Domain.Notifications;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Notifications;

/// <summary>Sends a text message. Not set up (no provider keys) means nothing is sent and deliveries are skipped.</summary>
/// <summary>Sends a phone alert (push). Not set up means nothing is sent and deliveries are skipped.</summary>
public interface IPushSender
{
    bool IsConfigured { get; }

    Task<Result> SendAsync(string token, string title, string body, CancellationToken cancellationToken);
}

public interface ISmsSender
{
    bool IsConfigured { get; }

    Task<Result> SendAsync(string to, string text, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task<Result> SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

public sealed record NotificationDto(
    Guid Id, string Tier, string Type, Dictionary<string, string> Title, Dictionary<string, string> Body, string? Link, bool Read, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListNotificationsQuery(bool UnreadOnly, string? Cursor, int? Limit) : IRequest<Result<CursorPage<NotificationDto>>>;

public sealed record UnreadCountQuery : IRequest<Result<int>>;

public sealed record MarkReadCommand(Guid? Id) : IRequest<Result<int>>;

public sealed record PreferenceDto(string Tier, bool InApp, bool Email);

public sealed record GetPreferencesQuery : IRequest<Result<IReadOnlyList<PreferenceDto>>>;

public sealed record SetPreferencesCommand(IReadOnlyList<PreferenceDto> Preferences) : IRequest<Result<IReadOnlyList<PreferenceDto>>>;

/// <summary>Raised for each person who has a new in-app notification, so their bell can update at once.</summary>
public sealed record NotificationCreated(Guid TenantId, Guid UserId) : INotification;

/// <summary>Scheduled job, per centre: sends waiting e-mails, retrying failures with growing pauses.</summary>
public sealed record RunNotificationDispatchCommand : IRequest<Result<int>>;

public sealed class SetPreferencesValidator : AbstractValidator<SetPreferencesCommand>
{
    public SetPreferencesValidator() =>
        RuleForEach(c => c.Preferences).Must(p => NotificationTiers.All.Contains(p.Tier)).WithMessage("Unknown tier.");
}

public static class DefaultChannels
{
    /// <summary>What happens when a person has not chosen: critical everywhere, operational in the app, informational by e-mail.</summary>
    public static (bool InApp, bool Email) For(string tier) => tier switch
    {
        NotificationTiers.Critical => (true, true),
        NotificationTiers.Operational => (true, false),
        _ => (false, true),
    };
}

/// <summary>Creates notifications for people, honouring their choices. Critical alerts always reach the app.</summary>
public sealed class Notifier(IAppDbContext db, ITenantContext tenant, IClock clock, IPublisher publisher)
{
    public async Task NotifyAsync(
        IReadOnlyCollection<Guid> userIds, string tier, string type, Dictionary<string, string> title, Dictionary<string, string> body, string? link, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var users = await db.AppUsers.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.Status == "active").Select(u => new { u.Id, u.Email }).ToListAsync(ct);
        var prefs = await db.NotificationPreferences.AsNoTracking().Where(p => userIds.Contains(p.UserId) && p.Tier == tier).ToListAsync(ct);
        var created = new List<Guid>();
        var smsOn = tier == NotificationTiers.Critical && await db.CentreSettings.AsNoTracking().Select(s => (bool?)s.SmsEnabled).FirstOrDefaultAsync(ct) == true;
        var phones = smsOn ? await PhonesAsync(userIds, ct) : [];
        var devices = (await db.DeviceTokens.AsNoTracking().Where(d => userIds.Contains(d.UserId)).Select(d => new { d.UserId, d.Token }).ToListAsync(ct)).ToLookup(d => d.UserId, d => d.Token);
        foreach (var user in users)
        {
            var pref = prefs.FirstOrDefault(p => p.UserId == user.Id);
            var (defIn, defMail) = DefaultChannels.For(tier);
            var inApp = tier == NotificationTiers.Critical || (pref?.InApp ?? defIn);
            var email = pref?.Email ?? defMail;
            if (!inApp && !email)
            {
                continue;
            }

            var notification = new Notification { UserId = user.Id, Tier = tier, Type = type, Title = title, Body = body, Link = link };
            db.Notifications.Add(notification);
            if (inApp)
            {
                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    NotificationId = notification.Id, Channel = NotificationChannels.InApp, Status = DeliveryStatuses.Sent,
                    NextAttemptAt = clock.UtcNow, SentAt = clock.UtcNow, Recipient = user.Id.ToString(),
                });
                created.Add(user.Id);
                foreach (var token in devices[user.Id])
                {
                    db.NotificationDeliveries.Add(new NotificationDelivery
                    {
                        NotificationId = notification.Id, Channel = NotificationChannels.Push, Status = DeliveryStatuses.Pending,
                        NextAttemptAt = clock.UtcNow, Recipient = token,
                    });
                }
            }

            if (email)
            {
                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    NotificationId = notification.Id, Channel = NotificationChannels.Email, Status = DeliveryStatuses.Pending,
                    NextAttemptAt = clock.UtcNow, Recipient = user.Email,
                });
            }

            if (smsOn && phones.TryGetValue(user.Id, out var phone))
            {
                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    NotificationId = notification.Id, Channel = NotificationChannels.Sms, Status = DeliveryStatuses.Pending,
                    NextAttemptAt = clock.UtcNow, Recipient = phone,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var id in created)
        {
            await publisher.Publish(new NotificationCreated(tenant.TenantId, id), ct);
        }
    }

    /// <summary>The mobile number on file for each person (staff or parent), in a form a text message can be sent to.</summary>
    private async Task<Dictionary<Guid, string>> PhonesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().Where(s => s.AppUserId != null && userIds.Contains(s.AppUserId.Value) && s.Phone != null).Select(s => new { UserId = s.AppUserId!.Value, s.Phone }).ToListAsync(ct);
        var parents = await db.Guardians.AsNoTracking().Where(g => g.AppUserId != null && userIds.Contains(g.AppUserId.Value) && g.Phone != null).Select(g => new { UserId = g.AppUserId!.Value, g.Phone }).ToListAsync(ct);
        var result = new Dictionary<Guid, string>();
        foreach (var p in staff.Concat(parents))
        {
            var digits = new string(p.Phone!.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (digits.StartsWith('+') && digits.Length is >= 9 and <= 16)
            {
                result.TryAdd(p.UserId, digits);
            }
        }

        return result;
    }

    /// <summary>Everyone at the centre whose role grants the permission (managers for shortages, and so on).</summary>
    public async Task<List<Guid>> UsersWithPermissionAsync(string permission, CancellationToken ct)
    {
        var roleNames = RolePermissions.AllRoleNames().Where(r => RolePermissions.ForRoles([r]).Contains(permission)).ToList();

        // Roles made in Settings keep their permissions in the database, so they are checked here as well.
        var custom = await db.Roles.AsNoTracking().Where(r => !r.IsSystemRole).ToListAsync(ct);
        roleNames.AddRange(custom.Where(r => r.Permissions.Contains(permission)).Select(r => r.Name));
        return await db.AppUserRoles.AsNoTracking().Where(ur => roleNames.Contains(ur.Role!.Name)).Select(ur => ur.AppUserId).Distinct().ToListAsync(ct);
    }
}

public sealed class ListNotificationsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListNotificationsQuery, Result<CursorPage<NotificationDto>>>
{
    public async Task<Result<CursorPage<NotificationDto>>> Handle(ListNotificationsQuery request, CancellationToken ct)
    {
        var me = current.UserId;
        var items = db.Notifications.AsNoTracking().Where(n => n.UserId == me
            && db.NotificationDeliveries.Any(d => d.NotificationId == n.Id && d.Channel == NotificationChannels.InApp));
        if (request.UnreadOnly)
        {
            items = items.Where(n => n.ReadAt == null);
        }

        return Result<CursorPage<NotificationDto>>.Success(await items.ToCursorPageAsync(request.Cursor, request.Limit,
            n => new NotificationDto(n.Id, n.Tier, n.Type, n.Title, n.Body, n.Link, n.ReadAt != null, n.CreatedAt), ct));
    }
}

public sealed class UnreadCountHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<UnreadCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(UnreadCountQuery request, CancellationToken ct)
    {
        var me = current.UserId;
        return Result<int>.Success(await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == me && n.ReadAt == null
            && db.NotificationDeliveries.Any(d => d.NotificationId == n.Id && d.Channel == NotificationChannels.InApp), ct));
    }
}

public sealed class MarkReadHandler(IAppDbContext db, ICurrentUser current, IClock clock) : IRequestHandler<MarkReadCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkReadCommand request, CancellationToken ct)
    {
        var me = current.UserId;
        var unread = db.Notifications.Where(n => n.UserId == me && n.ReadAt == null);
        if (request.Id is { } id)
        {
            unread = unread.Where(n => n.Id == id);
            if (!await db.Notifications.AnyAsync(n => n.Id == id && n.UserId == me, ct))
            {
                return Error.NotFound("Notification not found.");
            }
        }

        var rows = await unread.ToListAsync(ct);
        rows.ForEach(n => n.ReadAt = clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(rows.Count);
    }
}

public sealed record RegisterDeviceCommand(string Token, string? Platform) : IRequest<Result>;

public sealed record RemoveDeviceCommand(string Token) : IRequest<Result>;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Platform).Must(p => p is null or "android" or "ios" or "web").WithMessage("Platform must be android, ios or web.");
    }
}

/// <summary>Remembers a phone for alerts. The same phone signing in as someone else moves to the new person.</summary>
public sealed class RegisterDeviceHandler(IAppDbContext db, ICurrentUser current, IClock clock) : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        var row = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token, ct);
        if (row is null)
        {
            row = new DeviceToken { Token = request.Token };
            db.DeviceTokens.Add(row);
        }

        row.UserId = current.UserId;
        row.Platform = request.Platform ?? "android";
        row.LastSeenAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class RemoveDeviceHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<RemoveDeviceCommand, Result>
{
    public async Task<Result> Handle(RemoveDeviceCommand request, CancellationToken ct)
    {
        var rows = await db.DeviceTokens.Where(d => d.Token == request.Token && d.UserId == current.UserId).ToListAsync(ct);
        db.DeviceTokens.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class GetPreferencesHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<GetPreferencesQuery, Result<IReadOnlyList<PreferenceDto>>>
{
    public async Task<Result<IReadOnlyList<PreferenceDto>>> Handle(GetPreferencesQuery request, CancellationToken ct)
    {
        var me = current.UserId;
        var saved = await db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == me).ToListAsync(ct);
        return Result<IReadOnlyList<PreferenceDto>>.Success(NotificationTiers.All.Select(tier =>
        {
            var pref = saved.FirstOrDefault(p => p.Tier == tier);
            var (inApp, email) = DefaultChannels.For(tier);
            return new PreferenceDto(tier, tier == NotificationTiers.Critical || (pref?.InApp ?? inApp), pref?.Email ?? email);
        }).ToList());
    }
}

public sealed class SetPreferencesHandler(IAppDbContext db, ICurrentUser current, IMediator mediator)
    : IRequestHandler<SetPreferencesCommand, Result<IReadOnlyList<PreferenceDto>>>
{
    public async Task<Result<IReadOnlyList<PreferenceDto>>> Handle(SetPreferencesCommand request, CancellationToken ct)
    {
        var me = current.UserId;
        var saved = await db.NotificationPreferences.Where(p => p.UserId == me).ToListAsync(ct);
        foreach (var pref in request.Preferences)
        {
            var row = saved.FirstOrDefault(p => p.Tier == pref.Tier);
            if (row is null)
            {
                row = new NotificationPreference { UserId = me, Tier = pref.Tier };
                db.NotificationPreferences.Add(row);
            }

            row.InApp = pref.Tier == NotificationTiers.Critical || pref.InApp;
            row.Email = pref.Email;
        }

        await db.SaveChangesAsync(ct);
        return await mediator.Send(new GetPreferencesQuery(), ct);
    }
}

public sealed class RunNotificationDispatchHandler(IAppDbContext db, IEmailSender mail, ISmsSender sms, IPushSender push, IClock clock, CentreCalendar calendar) : IRequestHandler<RunNotificationDispatchCommand, Result<int>>
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    public async Task<Result<int>> Handle(RunNotificationDispatchCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.NotificationDeliveries
            .Where(d => (d.Channel == NotificationChannels.Email || d.Channel == NotificationChannels.Sms || d.Channel == NotificationChannels.Push) && d.Status == DeliveryStatuses.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt).Take(50).ToListAsync(ct);
        if (due.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var ids = due.Select(d => d.NotificationId).ToList();
        var notes = await db.Notifications.AsNoTracking().Where(n => ids.Contains(n.Id)).ToDictionaryAsync(n => n.Id, ct);
        var users = notes.Values.Select(n => n.UserId).Distinct().ToList();
        var languages = await db.AppUsers.AsNoTracking().Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.PreferredLanguage, ct);
        var sent = 0;

        // Text messages cost money, so each centre has a monthly limit.
        var monthStart = new DateTimeOffset(new DateTime(calendar.Today.Year, calendar.Today.Month, 1), TimeSpan.Zero);
        var quota = await db.CentreSettings.AsNoTracking().Select(s => (int?)s.SmsMonthlyQuota).FirstOrDefaultAsync(ct) ?? 200;
        var usedSms = await db.NotificationDeliveries.CountAsync(d => d.Channel == NotificationChannels.Sms && d.Status == DeliveryStatuses.Sent && d.SentAt >= monthStart, ct);

        foreach (var delivery in due)
        {
            var isSms = delivery.Channel == NotificationChannels.Sms;
            var isPush = delivery.Channel == NotificationChannels.Push;
            var configured = isSms ? sms.IsConfigured : isPush ? push.IsConfigured : mail.IsConfigured;
            if (!configured || string.IsNullOrWhiteSpace(delivery.Recipient) || !notes.TryGetValue(delivery.NotificationId, out var note))
            {
                delivery.Status = DeliveryStatuses.Skipped;
                delivery.Error = configured ? "No recipient" : isSms ? "Text messages are not set up" : isPush ? "Phone alerts are not set up" : "E-mail is not set up";
                continue;
            }

            if (isSms && usedSms >= quota)
            {
                delivery.Status = DeliveryStatuses.Skipped;
                delivery.Error = "The monthly text message limit has been reached";
                continue;
            }

            var lang = languages.GetValueOrDefault(note.UserId, "en");
            var result = isSms
                ? await sms.SendAsync(delivery.Recipient, $"{Pick(note.Title, lang)}: {Pick(note.Body, lang)}", ct)
                : isPush
                    ? await push.SendAsync(delivery.Recipient, Pick(note.Title, lang), Pick(note.Body, lang), ct)
                    : await mail.SendAsync(delivery.Recipient, Pick(note.Title, lang), Pick(note.Body, lang), ct);
            delivery.Attempts++;
            if (result.IsSuccess)
            {
                delivery.Status = DeliveryStatuses.Sent;
                delivery.SentAt = now;
                delivery.Error = null;
                sent++;
                if (isSms)
                {
                    usedSms++;
                }
            }
            else if (delivery.Attempts > (isSms ? 2 : Backoff.Length))
            {
                delivery.Status = DeliveryStatuses.Failed;
                delivery.Error = result.Errors.FirstOrDefault()?.Message;
            }
            else
            {
                delivery.NextAttemptAt = now + Backoff[Math.Min(delivery.Attempts - 1, Backoff.Length - 1)];
                delivery.Error = result.Errors.FirstOrDefault()?.Message;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(sent);
    }

    private static string Pick(Dictionary<string, string> text, string lang) =>
        text.TryGetValue(lang, out var v) ? v : text.GetValueOrDefault("en") ?? text.Values.FirstOrDefault() ?? string.Empty;
}

/// <summary>Turns system events into notifications.</summary>
public sealed class NotificationTriggers(Notifier notifier, IAppDbContext db, ITenantContext tenant) :
    INotificationHandler<RatioBreachDetected>,
    INotificationHandler<MessageSent>,
    INotificationHandler<DailyReportCreated>
{
    public async Task Handle(RatioBreachDetected n, CancellationToken ct)
    {
        tenant.TenantId = n.TenantId;
        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == n.RoomId).Select(r => r.Name).FirstOrDefaultAsync(ct) ?? new() { ["en"] = "A room" };
        string Name(string lang) => room.TryGetValue(lang, out var v) ? v : room.GetValueOrDefault("en") ?? "";
        var managers = await notifier.UsersWithPermissionAsync(Permissions.AttendanceOverride, ct);
        await notifier.NotifyAsync(managers, NotificationTiers.Critical, "ratio_breach",
            new() { ["en"] = $"{Name("en")} needs more staff", ["ar"] = $"{Name("ar")} تحتاج إلى مزيد من الموظفين" },
            new()
            {
                ["en"] = $"{n.StudentsPresent} children and {n.StaffPresent} staff are in the room; {n.StaffRequired} staff are needed.",
                ["ar"] = $"يوجد {n.StudentsPresent} طفلاً و{n.StaffPresent} موظفاً في الغرفة والمطلوب {n.StaffRequired} موظفين.",
            }, "/attendance", ct);
    }

    public async Task Handle(MessageSent n, CancellationToken ct)
    {
        tenant.TenantId = n.TenantId;
        await notifier.NotifyAsync(n.RecipientUserIds.ToList(), NotificationTiers.Operational, "message_received",
            new() { ["en"] = "You have a new message", ["ar"] = "لديك رسالة جديدة" },
            new() { ["en"] = "Open Messages to read it.", ["ar"] = "افتح الرسائل لقراءتها." }, "/messages", ct);
    }

    public async Task Handle(DailyReportCreated n, CancellationToken ct)
    {
        tenant.TenantId = n.TenantId;
        var users = await db.StudentGuardians.AsNoTracking()
            .Where(sg => sg.StudentId == n.StudentId && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);
        await notifier.NotifyAsync(users, NotificationTiers.Informational, "daily_report",
            new() { ["en"] = "Your child's daily report is ready", ["ar"] = "تقرير طفلكم اليومي جاهز" }, n.Summary, "/feed", ct);
    }
}

/// <summary>Raised after a child's daily report is written.</summary>
public sealed record DailyReportCreated(Guid TenantId, Guid StudentId, Dictionary<string, string> Summary) : INotification;
