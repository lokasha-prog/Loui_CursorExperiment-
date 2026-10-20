using System.Globalization;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed record MonthlyRow(Guid PersonId, string Name, int Present, int Late, int EarlyLeave, int Absent, int DaysRecorded);

public sealed record MonthlyReportDto(string Month, string PersonType, IReadOnlyList<MonthlyRow> Rows);

public sealed record MonthlyReportQuery(string Month, string PersonType) : IRequest<Result<MonthlyReportDto>>;

public sealed record ExportAttendanceQuery(string Month, string PersonType) : IRequest<Result<string>>;

public sealed record ForecastLine(Guid? ProgramId, Dictionary<string, string>? ProgramName, int Expected, int OnLeave, int Enrolled);

public sealed record ForecastDto(DateOnly Date, int TotalExpected, IReadOnlyList<ForecastLine> ByProgram, Dictionary<string, string>? Holiday = null);

public sealed record ForecastQuery(DateOnly? Date) : IRequest<Result<ForecastDto>>;

public sealed record EditAttendanceCommand(Guid Id, string Status, string? Notes) : IRequest<Result<AttendanceDto>>;

public sealed class MonthlyReportValidator : AbstractValidator<MonthlyReportQuery>
{
    public MonthlyReportValidator()
    {
        RuleFor(q => q.Month).Must(MonthRange.IsValid).WithMessage("Month must look like 2026-10.");
        RuleFor(q => q.PersonType).Must(t => PersonTypes.All.Contains(t)).WithMessage("Person type must be student or staff.");
    }
}

public sealed class ExportValidator : AbstractValidator<ExportAttendanceQuery>
{
    public ExportValidator()
    {
        RuleFor(q => q.Month).Must(MonthRange.IsValid).WithMessage("Month must look like 2026-10.");
        RuleFor(q => q.PersonType).Must(t => PersonTypes.All.Contains(t)).WithMessage("Person type must be student or staff.");
    }
}

public sealed class EditAttendanceValidator : AbstractValidator<EditAttendanceCommand>
{
    public EditAttendanceValidator() =>
        RuleFor(c => c.Status).Must(s => AttendanceStatuses.All.Contains(s) && s != AttendanceStatuses.Pending)
            .WithMessage("Choose a valid status.");
}

internal static class MonthRange
{
    public static bool IsValid(string? month) => DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out _);

    public static (DateOnly From, DateOnly To) Of(string month)
    {
        var from = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (from, from.AddMonths(1).AddDays(-1));
    }
}

internal static class MonthlyBuilder
{
    public static async Task<MonthlyReportDto> BuildAsync(IAppDbContext db, string month, string personType, CancellationToken ct)
    {
        var (from, to) = MonthRange.Of(month);
        var counts = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.PersonType == personType && a.Date >= from && a.Date <= to)
            .GroupBy(a => new { a.PersonId, a.Status })
            .Select(g => new { g.Key.PersonId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var ids = counts.Select(c => c.PersonId).Distinct().ToList();
        var names = personType == PersonTypes.Student
            ? await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToDictionaryAsync(s => s.Id, s => s.Name, ct)
            : await db.Staff.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName }).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var rows = ids.Select(id =>
        {
            int Of(string status) => counts.Where(c => c.PersonId == id && c.Status == status).Sum(c => c.Count);
            var all = counts.Where(c => c.PersonId == id).Sum(c => c.Count);
            return new MonthlyRow(id, names.GetValueOrDefault(id, "?"), Of(AttendanceStatuses.Present), Of(AttendanceStatuses.Late),
                Of(AttendanceStatuses.EarlyLeave), Of(AttendanceStatuses.Absent), all);
        }).OrderBy(r => r.Name).ToList();
        return new MonthlyReportDto(month, personType, rows);
    }
}

public sealed class MonthlyReportHandler(IAppDbContext db) : IRequestHandler<MonthlyReportQuery, Result<MonthlyReportDto>>
{
    public async Task<Result<MonthlyReportDto>> Handle(MonthlyReportQuery request, CancellationToken ct) =>
        Result<MonthlyReportDto>.Success(await MonthlyBuilder.BuildAsync(db, request.Month, request.PersonType, ct));
}

/// <summary>A sign-in sheet as CSV (opens in Excel). Cells that could be read as formulas are neutralised.</summary>
public sealed class ExportAttendanceHandler(IAppDbContext db) : IRequestHandler<ExportAttendanceQuery, Result<string>>
{
    public async Task<Result<string>> Handle(ExportAttendanceQuery request, CancellationToken ct)
    {
        var report = await MonthlyBuilder.BuildAsync(db, request.Month, request.PersonType, ct);
        var csv = new StringBuilder("Name,Present,Late,Left early,Absent,Days recorded\r\n");
        foreach (var r in report.Rows)
        {
            csv.Append(Cell(r.Name)).Append(',').Append(r.Present).Append(',').Append(r.Late).Append(',')
                .Append(r.EarlyLeave).Append(',').Append(r.Absent).Append(',').Append(r.DaysRecorded).Append("\r\n");
        }

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

public sealed class ForecastHandler(IAppDbContext db, CentreCalendar calendar, NMS.Application.Calendar.HolidayChecker holidays) : IRequestHandler<ForecastQuery, Result<ForecastDto>>
{
    public async Task<Result<ForecastDto>> Handle(ForecastQuery request, CancellationToken ct)
    {
        var date = request.Date ?? calendar.Today;
        if (await holidays.HolidayOnAsync(date, ct) is { } holiday)
        {
            return Result<ForecastDto>.Success(new ForecastDto(date, 0, [], holiday));
        }

        var students = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active)
            .Select(s => new { s.Id, s.ProgramId }).ToListAsync(ct);
        var onLeave = (await db.LeaveRequests.AsNoTracking()
            .Where(l => l.PersonType == PersonTypes.Student && l.Status == LeaveStatuses.Approved && l.StartDate <= date && l.EndDate >= date)
            .Select(l => l.PersonId).ToListAsync(ct)).ToHashSet();
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var lines = students.GroupBy(s => s.ProgramId).Select(g =>
        {
            var away = g.Count(s => onLeave.Contains(s.Id));
            return new ForecastLine(g.Key, g.Key is { } id ? programs.GetValueOrDefault(id) : null, g.Count() - away, away, g.Count());
        }).OrderByDescending(l => l.Expected).ToList();
        return Result<ForecastDto>.Success(new ForecastDto(date, lines.Sum(l => l.Expected), lines));
    }
}

/// <summary>Supervisor correction of a record. Every change is also written to the audit log by the audit interceptor.</summary>
public sealed class EditAttendanceHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<EditAttendanceCommand, Result<AttendanceDto>>
{
    public async Task<Result<AttendanceDto>> Handle(EditAttendanceCommand request, CancellationToken ct)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (record is null)
        {
            return Error.NotFound("Attendance record not found.");
        }

        if (request.Status == AttendanceStatuses.Absent && record.CheckInAt is not null)
        {
            return Error.Conflict("A person who was checked in cannot be changed to absent.");
        }

        record.Status = request.Status;
        record.Notes = request.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        var person = await PersonResolver.ResolveAsync(db, scope, null, record.PersonType, record.PersonId, ct);
        return Result<AttendanceDto>.Success(AttendanceMapping.ToDto(record, person?.Name));
    }
}
