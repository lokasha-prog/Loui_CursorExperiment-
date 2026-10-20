using NMS.Domain.Common;

namespace NMS.Domain.Calendar;

public static class EventTypes
{
    public const string Regular = "regular";
    public const string Holiday = "holiday";
    public const string StaffOnly = "staff_only";

    public static readonly string[] All = [Regular, Holiday, StaffOnly];
}

/// <summary>An event on the centre calendar: an outing, a holiday, a staff meeting. May repeat (a small subset of RFC 5545 RRULE).</summary>
public class CalendarEvent : TenantEntity
{
    public Dictionary<string, string> Title { get; set; } = [];

    public Dictionary<string, string>? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool IsAllDay { get; set; }

    public string Type { get; set; } = EventTypes.Regular;

    /// <summary>Empty means every room.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>"parents" (parents can see it) or "staff".</summary>
    public string Visibility { get; set; } = "parents";

    /// <summary>For example FREQ=WEEKLY;INTERVAL=1;BYDAY=TU,TH;UNTIL=20261231. Empty means it does not repeat.</summary>
    public string? Recurrence { get; set; }
}
