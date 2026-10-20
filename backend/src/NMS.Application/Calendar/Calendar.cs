using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Calendar;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Calendar;

public sealed record EventFields(
    Dictionary<string, string> Title, Dictionary<string, string>? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay,
    string Type, Guid? RoomId, string? Visibility, string? Recurrence);

public sealed record EventDto(
    Guid Id, Dictionary<string, string> Title, Dictionary<string, string>? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay,
    string Type, Guid? RoomId, string Visibility, string? Recurrence);

public sealed record OccurrenceDto(
    Guid EventId, Dictionary<string, string> Title, Dictionary<string, string>? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay,
    string Type, Guid? RoomId, string Visibility, bool Repeats);

public sealed record AgendaItem(string Kind, string? Label, Dictionary<string, string>? Title, DateOnly Start, DateOnly End, Guid? RefId, string? Detail);

public sealed record ListEventsQuery(DateOnly From, DateOnly To, Guid? RoomId, string? Type) : IRequest<Result<IReadOnlyList<OccurrenceDto>>>;

public sealed record GetEventQuery(Guid Id) : IRequest<Result<EventDto>>;

public sealed record SaveEventCommand(Guid? Id, EventFields Fields) : IRequest<Result<EventDto>>;

public sealed record DeleteEventCommand(Guid Id) : IRequest<Result>;

public sealed record AgendaQuery(DateOnly From, DateOnly To, IReadOnlyList<string> Layers, bool Mine) : IRequest<Result<IReadOnlyList<AgendaItem>>>;

public sealed record StaffScheduleRow(Guid StaffId, string Name, IReadOnlyList<StaffShift> Shifts);

public sealed record StaffShift(int DayOfWeek, Guid RoomId, string? RoomName);

public sealed record ProgramScheduleRow(Guid ProgramId, Dictionary<string, string> Name, TimeOnly StartTime, TimeOnly EndTime, int DaysPerWeek, int Students);

public sealed record ScheduleDto(IReadOnlyList<StaffScheduleRow> Staff, IReadOnlyList<ProgramScheduleRow> Programs);

public sealed record ScheduleQuery(Guid? RoomId) : IRequest<Result<ScheduleDto>>;

public static class AgendaLayers
{
    public const string Events = "events";
    public const string StudentBirthdays = "birthdays_students";
    public const string StaffBirthdays = "birthdays_staff";
    public const string Leaves = "leaves";
    public const string Tours = "tours";

    public static readonly string[] All = [Events, StudentBirthdays, StaffBirthdays, Leaves, Tours];
}

public sealed class SaveEventValidator : AbstractValidator<SaveEventCommand>
{
    public SaveEventValidator()
    {
        RuleFor(x => x.Fields.Title).Must(t => t is not null && t.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 200).WithMessage("Give the event an English title (up to 200 characters).");
        RuleFor(x => x.Fields.Type).Must(t => EventTypes.All.Contains(t)).WithMessage("Unknown event type.");
        RuleFor(x => x.Fields.Visibility).Must(v => v is null or "parents" or "staff").WithMessage("Visibility must be parents or staff.");
        RuleFor(x => x.Fields.EndsAt).GreaterThanOrEqualTo(x => x.Fields.StartsAt).WithMessage("The end cannot be before the start.");
        RuleFor(x => x.Fields.Recurrence).Must(r => RepeatRule.TryParse(r, out _)).WithMessage("That repeat rule is not supported.");
    }
}

public sealed class AgendaValidator : AbstractValidator<AgendaQuery>
{
    public AgendaValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 400).WithMessage("Choose a range of at most 13 months.");
        RuleFor(x => x.Layers).Must(l => l.All(AgendaLayers.All.Contains)).WithMessage("Unknown layer.");
    }
}

public sealed class ListEventsValidator : AbstractValidator<ListEventsQuery>
{
    public ListEventsValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 400).WithMessage("Choose a range of at most 13 months.");
    }
}

/// <summary>Turns stored events into the days they actually fall on, in the centre's time zone.</summary>
public sealed class EventExpander(CentreCalendar calendar)
{
    public TimeZoneInfo Zone => calendar.Zone;

    public IEnumerable<OccurrenceDto> Expand(CalendarEvent e, DateOnly from, DateOnly to)
    {
        var zone = calendar.Zone;
        var start = TimeZoneInfo.ConvertTime(e.StartsAt, zone);
        var end = TimeZoneInfo.ConvertTime(e.EndsAt, zone);
        var duration = end - start;
        RepeatRule.TryParse(e.Recurrence, out var rule);
        var firstDay = DateOnly.FromDateTime(start.DateTime);

        IEnumerable<DateOnly> days = rule is null ? [firstDay] : rule.Dates(firstDay, to);
        foreach (var day in days)
        {
            var s = new DateTimeOffset(day.ToDateTime(TimeOnly.FromDateTime(start.DateTime)), zone.GetUtcOffset(day.ToDateTime(TimeOnly.FromDateTime(start.DateTime))));
            var f = s + duration;
            if (DateOnly.FromDateTime(f.DateTime) < from || day > to)
            {
                continue;
            }

            yield return new OccurrenceDto(e.Id, e.Title, e.Description, s, f, e.IsAllDay, e.Type, e.RoomId, e.Visibility, rule is not null);
        }
    }
}

internal static class EventMapping
{
    public static EventDto ToDto(CalendarEvent e) => new(e.Id, e.Title, e.Description, e.StartsAt, e.EndsAt, e.IsAllDay, e.Type, e.RoomId, e.Visibility, e.Recurrence);

    /// <summary>What a person may see: families see only parent-visible events; staff see everything.</summary>
    public static async Task<IQueryable<CalendarEvent>> VisibleAsync(IQueryable<CalendarEvent> q, IAccessScope scope, CancellationToken ct) =>
        await scope.GetOwnStudentIdsAsync(ct) is not null ? q.Where(e => e.Visibility == "parents" && e.Type != EventTypes.StaffOnly) : q;
}

public sealed class ListEventsHandler(IAppDbContext db, IAccessScope scope, EventExpander expander) : IRequestHandler<ListEventsQuery, Result<IReadOnlyList<OccurrenceDto>>>
{
    public async Task<Result<IReadOnlyList<OccurrenceDto>>> Handle(ListEventsQuery request, CancellationToken ct)
    {
        var q = await EventMapping.VisibleAsync(db.CalendarEvents.AsNoTracking(), scope, ct);
        if (request.RoomId is { } room) q = q.Where(e => e.RoomId == null || e.RoomId == room);
        if (request.Type is not null) q = q.Where(e => e.Type == request.Type);
        var windowStart = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var windowEnd = new DateTimeOffset(request.To.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2);
        // One-off events can be filtered in the database; repeating ones start earlier and are expanded in memory.
        var events = await q.Where(e => e.Recurrence != null || (e.EndsAt >= windowStart && e.StartsAt <= windowEnd)).ToListAsync(ct);
        var list = events.SelectMany(e => expander.Expand(e, request.From, request.To)).OrderBy(o => o.StartsAt).ToList();
        return Result<IReadOnlyList<OccurrenceDto>>.Success(list);
    }
}

public sealed class GetEventHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetEventQuery, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(GetEventQuery request, CancellationToken ct)
    {
        var e = await (await EventMapping.VisibleAsync(db.CalendarEvents.AsNoTracking(), scope, ct)).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return e is null ? Error.NotFound("Event not found.") : Result<EventDto>.Success(EventMapping.ToDto(e));
    }
}

public sealed class SaveEventHandler(IAppDbContext db) : IRequestHandler<SaveEventCommand, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(SaveEventCommand request, CancellationToken ct)
    {
        var f = request.Fields;
        if (f.RoomId is { } room && !await db.Rooms.AnyAsync(r => r.Id == room, ct))
        {
            return Error.Validation("The room does not exist.", "room_id");
        }

        CalendarEvent e;
        if (request.Id is { } id)
        {
            var found = await db.CalendarEvents.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Event not found.");
            }

            e = found;
        }
        else
        {
            e = new CalendarEvent();
            db.CalendarEvents.Add(e);
        }

        e.Title = f.Title.Where(t => !string.IsNullOrWhiteSpace(t.Value)).ToDictionary(t => t.Key, t => t.Value.Trim());
        e.Description = f.Description?.Where(t => !string.IsNullOrWhiteSpace(t.Value)).ToDictionary(t => t.Key, t => t.Value.Trim());
        e.StartsAt = f.StartsAt;
        e.EndsAt = f.EndsAt;
        e.IsAllDay = f.IsAllDay;
        e.Type = f.Type;
        e.RoomId = f.RoomId;
        e.Visibility = f.Type == EventTypes.StaffOnly ? "staff" : f.Visibility ?? "parents";
        e.Recurrence = string.IsNullOrWhiteSpace(f.Recurrence) ? null : f.Recurrence.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct);
        return Result<EventDto>.Success(EventMapping.ToDto(e));
    }
}

public sealed class DeleteEventHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteEventCommand, Result>
{
    public async Task<Result> Handle(DeleteEventCommand request, CancellationToken ct)
    {
        var e = await db.CalendarEvents.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null)
        {
            return Result.Failure(Error.NotFound("Event not found."));
        }

        e.IsDeleted = true;
        e.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Whether the centre is closed on a date because of a holiday event, used by the attendance forecast.</summary>
public sealed class HolidayChecker(IAppDbContext db, EventExpander expander)
{
    public async Task<Dictionary<string, string>?> HolidayOnAsync(DateOnly date, CancellationToken ct)
    {
        var holidays = await db.CalendarEvents.AsNoTracking().Where(e => e.Type == EventTypes.Holiday && e.RoomId == null).ToListAsync(ct);
        return holidays.SelectMany(h => expander.Expand(h, date, date)).FirstOrDefault()?.Title;
    }
}

/// <summary>The admin and personal calendars: one list of everything happening, in layers the person can switch on or off.</summary>
public sealed class AgendaHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, EventExpander expander) : IRequestHandler<AgendaQuery, Result<IReadOnlyList<AgendaItem>>>
{
    public async Task<Result<IReadOnlyList<AgendaItem>>> Handle(AgendaQuery request, CancellationToken ct)
    {
        var layers = request.Layers.Count == 0 ? AgendaLayers.All : request.Layers;
        var items = new List<AgendaItem>();
        Guid? myStaff = request.Mine ? await db.Staff.AsNoTracking().Where(s => s.AppUserId == current.UserId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct) : null;

        if (layers.Contains(AgendaLayers.Events))
        {
            var events = await (await EventMapping.VisibleAsync(db.CalendarEvents.AsNoTracking(), scope, ct)).ToListAsync(ct);
            items.AddRange(events.SelectMany(e => expander.Expand(e, request.From, request.To))
                .Select(o => new AgendaItem(o.Type == EventTypes.Holiday ? "holiday" : "event", null, o.Title, DateOnly.FromDateTime(o.StartsAt.DateTime), DateOnly.FromDateTime(o.EndsAt.DateTime), o.EventId, o.Type)));
        }

        if (layers.Contains(AgendaLayers.StudentBirthdays))
        {
            var kids = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.DateOfBirth }).ToListAsync(ct);
            items.AddRange(Birthdays(kids.Select(k => (k.Id, k.Name, k.DateOfBirth)), AgendaLayers.StudentBirthdays, request.From, request.To));
        }

        if (layers.Contains(AgendaLayers.StaffBirthdays) && await scope.GetOwnStudentIdsAsync(ct) is null)
        {
            var staff = await db.Staff.AsNoTracking().Where(s => s.DateOfBirth != null && (myStaff == null || s.Id == myStaff))
                .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.DateOfBirth }).ToListAsync(ct);
            items.AddRange(Birthdays(staff.Select(s => (s.Id, s.Name, s.DateOfBirth!.Value)), AgendaLayers.StaffBirthdays, request.From, request.To));
        }

        if (layers.Contains(AgendaLayers.Leaves) && await scope.GetOwnStudentIdsAsync(ct) is null)
        {
            var leaves = await db.LeaveRequests.AsNoTracking()
                .Where(l => l.PersonType == PersonTypes.Staff && l.Status != LeaveStatuses.Rejected && l.Status != LeaveStatuses.Cancelled && l.StartDate <= request.To && l.EndDate >= request.From && (myStaff == null || l.PersonId == myStaff))
                .Select(l => new { l.Id, l.StartDate, l.EndDate, l.Status, Name = db.Staff.Where(s => s.Id == l.PersonId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault() })
                .ToListAsync(ct);
            items.AddRange(leaves.Select(l => new AgendaItem("leave", l.Name, null, l.StartDate, l.EndDate, l.Id, l.Status)));
        }

        if (layers.Contains(AgendaLayers.Tours) && await scope.GetOwnStudentIdsAsync(ct) is null)
        {
            var from = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
            var to = new DateTimeOffset(request.To.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2);
            var tours = await db.Tours.AsNoTracking().Where(t => t.Status == TourStatuses.Scheduled && t.StartsAt >= from && t.StartsAt < to && (myStaff == null || t.StaffId == myStaff))
                .Select(t => new { t.Id, t.StartsAt, Family = db.Enquiries.Where(e => e.Id == t.EnquiryId).Select(e => e.FamilyName).FirstOrDefault() }).ToListAsync(ct);
            items.AddRange(tours
                .Select(t => new { t.Id, t.Family, Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t.StartsAt, expander.Zone).DateTime) })
                .Where(t => t.Day >= request.From && t.Day <= request.To)
                .Select(t => new AgendaItem("tour", t.Family, null, t.Day, t.Day, t.Id, null)));
        }

        return Result<IReadOnlyList<AgendaItem>>.Success(items.OrderBy(i => i.Start).ThenBy(i => i.Kind).ToList());
    }

    private static IEnumerable<AgendaItem> Birthdays(IEnumerable<(Guid Id, string Name, DateOnly Born)> people, string kind, DateOnly from, DateOnly to)
    {
        foreach (var (id, name, born) in people)
        {
            for (var year = from.Year; year <= to.Year; year++)
            {
                var day = born.Month == 2 && born.Day == 29 && !DateTime.IsLeapYear(year) ? new DateOnly(year, 2, 28) : new DateOnly(year, born.Month, born.Day);
                if (day >= from && day <= to && year > born.Year)
                {
                    yield return new AgendaItem(kind, name, null, day, day, id, (year - born.Year).ToString());
                }
            }
        }
    }
}

public sealed class ScheduleHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<ScheduleQuery, Result<ScheduleDto>>
{
    public async Task<Result<ScheduleDto>> Handle(ScheduleQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name.GetValueOrDefault("en"), ct);
        var assignments = db.StaffRoomAssignments.AsNoTracking().Where(a => a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today));
        if (request.RoomId is { } room) assignments = assignments.Where(a => a.RoomId == room);
        var rows = await assignments.Select(a => new { a.StaffId, a.DayOfWeek, a.RoomId, Name = db.Staff.Where(s => s.Id == a.StaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault() }).ToListAsync(ct);
        var staff = rows.GroupBy(r => r.StaffId).Select(g => new StaffScheduleRow(g.Key, g.First().Name ?? string.Empty, g.OrderBy(x => x.DayOfWeek).Select(x => new StaffShift(x.DayOfWeek, x.RoomId, rooms.GetValueOrDefault(x.RoomId))).ToList()))
            .OrderBy(s => s.Name).ToList();

        var kids = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
        if (request.RoomId is { } r2) kids = kids.Where(s => s.RoomId == r2);
        var counts = await kids.Where(s => s.ProgramId != null).GroupBy(s => s.ProgramId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        var programs = (await db.Programs.AsNoTracking().OrderBy(p => p.StartTime).ToListAsync(ct))
            .Select(p => new ProgramScheduleRow(p.Id, p.Name, p.StartTime, p.EndTime, p.DaysPerWeek, counts.GetValueOrDefault(p.Id))).ToList();
        return Result<ScheduleDto>.Success(new ScheduleDto(staff, programs));
    }
}
