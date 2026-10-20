using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NMS.Application.Attendance;
using NMS.Application.Security;

namespace NMS.Api.Realtime;

public static class RealtimeGroups
{
    /// <summary>Everyone at a centre who works with attendance (teachers, reception, managers).</summary>
    public static string Staff(Guid tenantId) => $"centre:{tenantId:N}:staff";

    /// <summary>People who may approve and override, who also receive staff-shortage alerts.</summary>
    public static string Managers(Guid tenantId) => $"centre:{tenantId:N}:managers";
}

public static class RealtimeEvents
{
    public const string AttendanceChanged = "attendance_changed";
    public const string RatioBreach = "ratio_breach";
    public const string RatioRestored = "ratio_restored";
}

/// <summary>
/// Live updates for one centre. A connection joins only its own centre's groups, taken from the signed token, so nothing
/// can cross between centres. Parents are not added: they get no staff-facing events.
/// </summary>
[Authorize]
public sealed class CentreHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is null || !Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId))
        {
            Context.Abort();
            return;
        }

        var permissions = RolePermissions.ForRoles(user.FindAll("role").Select(c => c.Value));
        if (permissions.Contains(Permissions.AttendanceWrite))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Staff(tenantId));
        }

        if (permissions.Contains(Permissions.AttendanceOverride))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Managers(tenantId));
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>Turns in-app events into messages for connected screens.</summary>
public sealed class RealtimeNotifier(IHubContext<CentreHub> hub) :
    INotificationHandler<AttendanceChanged>,
    INotificationHandler<RatioBreachDetected>,
    INotificationHandler<RatioRestored>
{
    public Task Handle(AttendanceChanged notification, CancellationToken cancellationToken) =>
        hub.Clients.Group(RealtimeGroups.Staff(notification.TenantId)).SendAsync(
            RealtimeEvents.AttendanceChanged,
            new { notification.PersonType, notification.RoomId, notification.Status },
            cancellationToken);

    public Task Handle(RatioBreachDetected notification, CancellationToken cancellationToken) =>
        hub.Clients.Group(RealtimeGroups.Staff(notification.TenantId)).SendAsync(
            RealtimeEvents.RatioBreach,
            new { notification.RoomId, notification.StudentsPresent, notification.StaffPresent, notification.StaffRequired },
            cancellationToken);

    public Task Handle(RatioRestored notification, CancellationToken cancellationToken) =>
        hub.Clients.Group(RealtimeGroups.Staff(notification.TenantId)).SendAsync(
            RealtimeEvents.RatioRestored, new { notification.RoomId }, cancellationToken);
}

public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    /// <summary>
    /// Optional Redis connection string. Leave empty to run on one server; set it to share live updates between several servers.
    /// </summary>
    public string? RedisConnectionString { get; set; }
}
