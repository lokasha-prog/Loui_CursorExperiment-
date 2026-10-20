using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed class AttendanceOptions
{
    public const string Section = "Attendance";

    /// <summary>The centre's local time zone, used to decide what "today" is (IANA id).</summary>
    public string TimeZoneId { get; set; } = "Asia/Dubai";

    /// <summary>Minutes after the programme start before a check-in is marked late.</summary>
    public int LateGraceMinutes { get; set; } = 15;

    /// <summary>Minutes before the programme end that still count as a normal pick-up.</summary>
    public int EarlyLeaveGraceMinutes { get; set; } = 15;
}

/// <summary>Works out the centre's local date and time so "today" is never the UTC day.</summary>
public sealed class CentreCalendar(IClock clock, IOptions<AttendanceOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    public TimeZoneInfo Zone => _zone;

    public DateTimeOffset Now => clock.UtcNow;

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(clock.UtcNow, _zone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public TimeOnly LocalTime => TimeOnly.FromDateTime(LocalNow);
}

public sealed record AttendanceDto(
    Guid Id,
    string PersonType,
    Guid PersonId,
    string? PersonName,
    Guid? RoomId,
    DateOnly Date,
    DateTimeOffset? CheckInAt,
    string? CheckInMethod,
    DateTimeOffset? CheckOutAt,
    string? CheckOutMethod,
    Guid? CheckOutGuardianId,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record CheckInCommand(string? Barcode, string? PersonType, Guid? PersonId, Guid? RoomId, string? Device)
    : IRequest<Result<AttendanceDto>>;

public sealed record CheckOutCommand(
    string? Barcode, string? PersonType, Guid? PersonId, Guid? GuardianId, bool? PickupIdentityVerified, string? Device)
    : IRequest<Result<AttendanceDto>>;

public sealed record MarkAbsentCommand(Guid StudentId, string? Notes, DateOnly? Date) : IRequest<Result<AttendanceDto>>;

public sealed record ListAttendanceQuery(DateOnly? Date, Guid? RoomId, string? PersonType, string? Cursor, int? Limit)
    : IRequest<Result<CursorPage<AttendanceDto>>>;

/// <summary>Raised after a check-in or check-out leaves a room with too few staff (spec 02 §3.3).</summary>
public sealed record RatioBreachDetected(Guid TenantId, Guid RoomId, int StudentsPresent, int StaffPresent, int StaffRequired)
    : INotification;

/// <summary>Raised when a room that was short of staff has enough again.</summary>
public sealed record RatioRestored(Guid TenantId, Guid RoomId) : INotification;

/// <summary>Raised after any attendance change so live screens can refresh.</summary>
public sealed record AttendanceChanged(Guid TenantId, string PersonType, Guid PersonId, Guid? RoomId, string Status)
    : INotification;

public sealed class CheckInValidator : AbstractValidator<CheckInCommand>
{
    public CheckInValidator()
    {
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(c.Barcode) || (c.PersonId is not null && c.PersonType is not null))
            .WithMessage("Scan a barcode or choose a person.");
        RuleFor(c => c.PersonType!).Must(t => PersonTypes.All.Contains(t)).When(c => c.PersonType is not null)
            .WithMessage("Person type must be student or staff.");
        RuleFor(c => c.Barcode!).MaximumLength(100).When(c => c.Barcode is not null);
    }
}

public sealed class CheckOutValidator : AbstractValidator<CheckOutCommand>
{
    public CheckOutValidator()
    {
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(c.Barcode) || (c.PersonId is not null && c.PersonType is not null))
            .WithMessage("Scan a barcode or choose a person.");
        RuleFor(c => c.PersonType!).Must(t => PersonTypes.All.Contains(t)).When(c => c.PersonType is not null)
            .WithMessage("Person type must be student or staff.");
        RuleFor(c => c.Barcode!).MaximumLength(100).When(c => c.Barcode is not null);
    }
}

public sealed class ListAttendanceValidator : AbstractValidator<ListAttendanceQuery>
{
    public ListAttendanceValidator() =>
        RuleFor(q => q.PersonType!).Must(t => PersonTypes.All.Contains(t)).When(q => q.PersonType is not null)
            .WithMessage("Person type must be student or staff.");
}

internal sealed record ResolvedPerson(string Type, Guid Id, Guid? RoomId, string Name, Guid? ProgramId);

internal static class PersonResolver
{
    /// <summary>Finds the person by barcode or id, honouring room scope. Returns null when nobody visible matches.</summary>
    public static async Task<ResolvedPerson?> ResolveAsync(
        IAppDbContext db, IAccessScope scope, string? barcode, string? type, Guid? id, CancellationToken ct)
    {
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        var code = barcode?.Trim();

        if (type is null or PersonTypes.Student)
        {
            var students = db.Students.AsNoTracking().Where(s => code != null ? s.Barcode == code : s.Id == id);
            if (rooms is not null)
            {
                students = students.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
            }

            var student = await students.Select(s => new { s.Id, s.RoomId, s.ProgramId, s.FirstName, s.LastName, s.Status }).FirstOrDefaultAsync(ct);
            if (student is not null)
            {
                return new ResolvedPerson(PersonTypes.Student, student.Id, student.RoomId, $"{student.FirstName} {student.LastName}", student.ProgramId);
            }
        }

        // Staff are not tied to one room, so only unrestricted users (not room-scoped teachers) may check them in.
        if (type is null or PersonTypes.Staff && rooms is null)
        {
            var staff = await db.Staff.AsNoTracking().Where(s => code != null ? s.Barcode == code : s.Id == id)
                .Select(s => new { s.Id, s.FirstName, s.LastName }).FirstOrDefaultAsync(ct);
            if (staff is not null && rooms is null)
            {
                return new ResolvedPerson(PersonTypes.Staff, staff.Id, null, $"{staff.FirstName} {staff.LastName}", null);
            }
        }

        return null;
    }

    public static async Task<string?> InactiveReasonAsync(IAppDbContext db, ResolvedPerson person, CancellationToken ct)
    {
        if (person.Type == PersonTypes.Student)
        {
            var status = await db.Students.AsNoTracking().Where(s => s.Id == person.Id).Select(s => s.Status).FirstAsync(ct);
            return status == StudentStatuses.Active ? null : "This student is not active.";
        }

        var staffStatus = await db.Staff.AsNoTracking().Where(s => s.Id == person.Id).Select(s => s.Status).FirstAsync(ct);
        return staffStatus == "active" ? null : "This staff member is not active.";
    }
}

internal static class AttendanceMapping
{
    public static AttendanceDto ToDto(AttendanceRecord a, string? name) => new(
        a.Id, a.PersonType, a.PersonId, name, a.RoomId, a.Date, a.CheckInAt, a.CheckInMethod, a.CheckOutAt,
        a.CheckOutMethod, a.CheckOutGuardianId, a.Status, a.Notes, a.CreatedAt);
}

public sealed class CheckInHandler(
    IAppDbContext db,
    IAccessScope scope,
    ICurrentUser current,
    CentreCalendar calendar,
    ITenantContext tenant,
    IClock clock,
    NMS.Application.Settings.CentreSettingsReader settings,
    IPublisher publisher) : IRequestHandler<CheckInCommand, Result<AttendanceDto>>
{
    public async Task<Result<AttendanceDto>> Handle(CheckInCommand request, CancellationToken ct)
    {
        var method = string.IsNullOrWhiteSpace(request.Barcode) ? CheckMethods.Manual : CheckMethods.Barcode;
        var person = await PersonResolver.ResolveAsync(db, scope, request.Barcode, request.PersonType, request.PersonId, ct);
        if (person is null)
        {
            await Log(request, null, AttendanceEventTypes.FailedLookup, AttendanceOutcomes.Error, "No matching person.", ct);
            return Error.NotFound("No matching person was found.");
        }

        if (await PersonResolver.InactiveReasonAsync(db, person, ct) is { } inactive)
        {
            await Log(request, person, AttendanceEventTypes.CheckIn, AttendanceOutcomes.Error, inactive, ct);
            return Error.InvalidState(inactive);
        }

        var today = calendar.Today;
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(
            a => a.PersonType == person.Type && a.PersonId == person.Id && a.Date == today, ct);

        if (record?.CheckInAt is not null)
        {
            var already = record.CheckOutAt is null ? AttendanceEventTypes.AlreadyCheckedIn : AttendanceEventTypes.AlreadyCheckedOut;
            var message = record.CheckOutAt is null ? "Already checked in today." : "Already checked out today.";
            await Log(request, person, already, AttendanceOutcomes.Warning, message, ct);
            return Error.Conflict(message);
        }

        var roomId = person.RoomId ?? request.RoomId ?? await TodaysStaffRoomAsync(person, today, ct);
        var status = await IsLateAsync(person, ct) ? AttendanceStatuses.Late : AttendanceStatuses.Present;

        if (record is null)
        {
            record = new AttendanceRecord { PersonType = person.Type, PersonId = person.Id, Date = today };
            db.AttendanceRecords.Add(record);
        }

        record.RoomId = roomId;
        record.CheckInAt = calendar.Now;
        record.CheckInBy = current.UserId;
        record.CheckInMethod = method;
        record.Status = status;
        record.Notes = null;

        db.AttendanceEvents.Add(NewEvent(request.Barcode, person, AttendanceEventTypes.CheckIn, AttendanceOutcomes.Success, null, request.Device));
        await db.SaveChangesAsync(ct);
        await RatioMonitor.PublishAsync(db, publisher, clock, tenant.TenantId, today, roomId, person, status, ct);
        return Result<AttendanceDto>.Success(AttendanceMapping.ToDto(record, person.Name));
    }

    private async Task<Guid?> TodaysStaffRoomAsync(ResolvedPerson person, DateOnly today, CancellationToken ct)
    {
        if (person.Type != PersonTypes.Staff)
        {
            return null;
        }

        var day = (int)today.DayOfWeek;
        var rooms = await db.StaffRoomAssignments.AsNoTracking()
            .Where(a => a.StaffId == person.Id && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderByDescending(a => a.DayOfWeek == day)
            .Select(a => (Guid?)a.RoomId)
            .FirstOrDefaultAsync(ct);
        return rooms;
    }

    private async Task<bool> IsLateAsync(ResolvedPerson person, CancellationToken ct)
    {
        if (person.Type != PersonTypes.Student || person.ProgramId is null)
        {
            return false;
        }

        var start = await db.Programs.AsNoTracking().Where(p => p.Id == person.ProgramId).Select(p => (TimeOnly?)p.StartTime).FirstOrDefaultAsync(ct);
        return start is not null && calendar.LocalTime > start.Value.AddMinutes((await settings.AttendanceAsync(ct)).LateGrace);
    }

    private async Task Log(CheckInCommand request, ResolvedPerson? person, string type, string outcome, string message, CancellationToken ct)
    {
        db.AttendanceEvents.Add(NewEvent(request.Barcode ?? request.PersonId?.ToString(), person, type, outcome, message, request.Device));
        await db.SaveChangesAsync(ct);
    }

    private AttendanceEvent NewEvent(string? scanned, ResolvedPerson? person, string type, string outcome, string? message, string? device) => new()
    {
        PersonType = person?.Type ?? PersonTypes.Student,
        PersonId = person?.Id,
        BarcodeScanned = string.IsNullOrWhiteSpace(scanned) ? "manual" : scanned.Trim(),
        EventType = type,
        ScannedAt = calendar.Now,
        ScannedBy = current.UserId,
        ScannerDevice = device,
        IpAddress = current.IpAddress,
        Outcome = outcome,
        Message = message,
    };
}

public sealed class CheckOutHandler(
    IAppDbContext db,
    IAccessScope scope,
    ICurrentUser current,
    CentreCalendar calendar,
    ITenantContext tenant,
    IClock clock,
    NMS.Application.Settings.CentreSettingsReader settings,
    IPublisher publisher) : IRequestHandler<CheckOutCommand, Result<AttendanceDto>>
{
    public async Task<Result<AttendanceDto>> Handle(CheckOutCommand request, CancellationToken ct)
    {
        var method = string.IsNullOrWhiteSpace(request.Barcode) ? CheckMethods.Manual : CheckMethods.Barcode;
        var person = await PersonResolver.ResolveAsync(db, scope, request.Barcode, request.PersonType, request.PersonId, ct);
        if (person is null)
        {
            await Log(request, null, AttendanceEventTypes.FailedLookup, AttendanceOutcomes.Error, "No matching person.", ct);
            return Error.NotFound("No matching person was found.");
        }

        var today = calendar.Today;
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(
            a => a.PersonType == person.Type && a.PersonId == person.Id && a.Date == today, ct);

        if (record?.CheckInAt is null)
        {
            await Log(request, person, AttendanceEventTypes.CheckOut, AttendanceOutcomes.Error, "Not checked in today.", ct);
            return Error.InvalidState("This person has not been checked in today.");
        }

        if (record.CheckOutAt is not null)
        {
            await Log(request, person, AttendanceEventTypes.AlreadyCheckedOut, AttendanceOutcomes.Warning, "Already checked out today.", ct);
            return Error.Conflict("Already checked out today.");
        }

        if (person.Type == PersonTypes.Student)
        {
            var problem = await ValidatePickupAsync(person.Id, request, ct);
            if (problem is not null)
            {
                await Log(request, person, AttendanceEventTypes.CheckOut, AttendanceOutcomes.Error, problem.Message, ct);
                return problem;
            }

            record.CheckOutGuardianId = request.GuardianId;
            record.PickupIdentityVerified = request.PickupIdentityVerified;
        }

        record.CheckOutAt = calendar.Now;
        record.CheckOutBy = current.UserId;
        record.CheckOutMethod = method;
        if (record.Status == AttendanceStatuses.Present && await LeavesEarlyAsync(person, ct))
        {
            record.Status = AttendanceStatuses.EarlyLeave;
        }

        db.AttendanceEvents.Add(new AttendanceEvent
        {
            PersonType = person.Type,
            PersonId = person.Id,
            BarcodeScanned = string.IsNullOrWhiteSpace(request.Barcode) ? "manual" : request.Barcode.Trim(),
            EventType = AttendanceEventTypes.CheckOut,
            ScannedAt = calendar.Now,
            ScannedBy = current.UserId,
            ScannerDevice = request.Device,
            IpAddress = current.IpAddress,
            Outcome = AttendanceOutcomes.Success,
        });
        await db.SaveChangesAsync(ct);
        await RatioMonitor.PublishAsync(db, publisher, clock, tenant.TenantId, today, record.RoomId, person, record.Status, ct);
        return Result<AttendanceDto>.Success(AttendanceMapping.ToDto(record, person.Name));
    }

    /// <summary>A child may only be released to an authorised guardian whose identity was checked (spec 02 §3.3).</summary>
    private async Task<Error?> ValidatePickupAsync(Guid studentId, CheckOutCommand request, CancellationToken ct)
    {
        if (request.GuardianId is null)
        {
            return Error.Validation("Choose who is collecting the child.", "guardian_id");
        }

        if (request.PickupIdentityVerified != true)
        {
            return Error.Validation("Confirm that the identity of the person collecting the child was checked.", "pickup_identity_verified");
        }

        var allowed = await db.StudentGuardians.AsNoTracking()
            .AnyAsync(sg => sg.StudentId == studentId && sg.GuardianId == request.GuardianId && sg.Guardian!.CanPickup, ct);
        return allowed ? null : Error.Validation("This person is not authorised to collect the child.", "guardian_id");
    }

    private async Task<bool> LeavesEarlyAsync(ResolvedPerson person, CancellationToken ct)
    {
        if (person.Type != PersonTypes.Student || person.ProgramId is null)
        {
            return false;
        }

        var end = await db.Programs.AsNoTracking().Where(p => p.Id == person.ProgramId).Select(p => (TimeOnly?)p.EndTime).FirstOrDefaultAsync(ct);
        return end is not null && calendar.LocalTime < end.Value.AddMinutes(-(await settings.AttendanceAsync(ct)).EarlyGrace);
    }

    private async Task Log(CheckOutCommand request, ResolvedPerson? person, string type, string outcome, string message, CancellationToken ct)
    {
        db.AttendanceEvents.Add(new AttendanceEvent
        {
            PersonType = person?.Type ?? PersonTypes.Student,
            PersonId = person?.Id,
            BarcodeScanned = string.IsNullOrWhiteSpace(request.Barcode) ? "manual" : request.Barcode.Trim(),
            EventType = type,
            ScannedAt = calendar.Now,
            ScannedBy = current.UserId,
            ScannerDevice = request.Device,
            IpAddress = current.IpAddress,
            Outcome = outcome,
            Message = message,
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class MarkAbsentHandler(
    IAppDbContext db, IAccessScope scope, CentreCalendar calendar, ITenantContext tenant, IPublisher publisher)
    : IRequestHandler<MarkAbsentCommand, Result<AttendanceDto>>
{
    public async Task<Result<AttendanceDto>> Handle(MarkAbsentCommand request, CancellationToken ct)
    {
        var date = request.Date ?? calendar.Today;
        if (date > calendar.Today)
        {
            return Error.Validation("Absence cannot be recorded for a future date.", "date");
        }

        var person = await PersonResolver.ResolveAsync(db, scope, null, PersonTypes.Student, request.StudentId, ct);
        if (person is null || person.Type != PersonTypes.Student)
        {
            return Error.NotFound("Student not found.");
        }

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(
            a => a.PersonType == PersonTypes.Student && a.PersonId == person.Id && a.Date == date, ct);
        if (record?.CheckInAt is not null)
        {
            return Error.Conflict("This student has already been checked in on that day.");
        }

        if (record is null)
        {
            record = new AttendanceRecord { PersonType = PersonTypes.Student, PersonId = person.Id, Date = date };
            db.AttendanceRecords.Add(record);
        }

        record.RoomId = person.RoomId;
        record.Status = AttendanceStatuses.Absent;
        record.Notes = request.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new AttendanceChanged(tenant.TenantId, PersonTypes.Student, person.Id, person.RoomId, record.Status), ct);
        return Result<AttendanceDto>.Success(AttendanceMapping.ToDto(record, person.Name));
    }
}

public sealed class ListAttendanceHandler(IAppDbContext db, IAccessScope scope, CentreCalendar calendar)
    : IRequestHandler<ListAttendanceQuery, Result<CursorPage<AttendanceDto>>>
{
    public async Task<Result<CursorPage<AttendanceDto>>> Handle(ListAttendanceQuery request, CancellationToken ct)
    {
        var date = request.Date ?? calendar.Today;
        var records = db.AttendanceRecords.AsNoTracking().Where(a => a.Date == date);
        if (request.RoomId is { } roomId)
        {
            records = records.Where(a => a.RoomId == roomId);
        }

        if (request.PersonType is { } type)
        {
            records = records.Where(a => a.PersonType == type);
        }

        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            records = records.Where(a => a.PersonType == PersonTypes.Student && own.Contains(a.PersonId));
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            records = records.Where(a => a.PersonType == PersonTypes.Student && a.RoomId != null && rooms.Contains(a.RoomId.Value));
        }

        var page = await records.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            a => new AttendanceDto(
                a.Id, a.PersonType, a.PersonId,
                a.PersonType == PersonTypes.Student
                    ? db.Students.Where(s => s.Id == a.PersonId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault()
                    : db.Staff.Where(s => s.Id == a.PersonId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
                a.RoomId, a.Date, a.CheckInAt, a.CheckInMethod, a.CheckOutAt, a.CheckOutMethod, a.CheckOutGuardianId,
                a.Status, a.Notes, a.CreatedAt),
            ct);
        return Result<CursorPage<AttendanceDto>>.Success(page);
    }
}
