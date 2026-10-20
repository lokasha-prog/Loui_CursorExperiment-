using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Security;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.StaffMembers;

public sealed record StaffSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? JobTitle,
    string Status,
    DateOnly HireDate,
    string Barcode,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record StaffDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    Dictionary<string, string>? DisplayName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Email,
    string? Phone,
    string? JobTitle,
    DateOnly HireDate,
    DateOnly? EndDate,
    string Status,
    string Barcode,
    string? EmiratesId,
    string? PassportNumber,
    long? SalaryAmountFils,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record StaffFields(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Email,
    string? Phone,
    string? JobTitle,
    DateOnly HireDate,
    DateOnly? EndDate,
    string Status,
    string? EmiratesId,
    string? PassportNumber,
    long? SalaryAmountFils);

public sealed record RoomAssignmentDto(
    Guid Id,
    Guid StaffId,
    Guid RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListStaffQuery(string? Cursor, int? Limit, string? Status, string? Search)
    : IRequest<Result<CursorPage<StaffSummaryDto>>>;

public sealed record GetStaffQuery(Guid Id) : IRequest<Result<StaffDetailDto>>;

public sealed record CreateStaffCommand(StaffFields Staff) : IRequest<Result<StaffDetailDto>>;

public sealed record UpdateStaffCommand(Guid Id, StaffFields Staff) : IRequest<Result<StaffDetailDto>>;

public sealed record DeleteStaffCommand(Guid Id) : IRequest<Result>;

public sealed record ListRoomAssignmentsQuery(Guid StaffId) : IRequest<Result<IReadOnlyList<RoomAssignmentDto>>>;

public sealed record CreateRoomAssignmentCommand(
    Guid StaffId,
    Guid RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : IRequest<Result<RoomAssignmentDto>>;

public sealed class StaffFieldsValidator : AbstractValidator<StaffFields>
{
    public StaffFieldsValidator()
    {
        RuleFor(s => s.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(s => s.LastName).NotEmpty().MaximumLength(100);
        RuleFor(s => s.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(s => s.Status).Must(s => StaffStatuses.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(s => s.EndDate)
            .Must((s, end) => end is null || end >= s.HireDate).WithMessage("End date cannot be before the hire date.");
        RuleFor(s => s.SalaryAmountFils)
            .Must(v => v is null or >= 0).WithMessage("Salary cannot be negative.");
    }
}

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator() => RuleFor(c => c.Staff).NotNull().SetValidator(new StaffFieldsValidator());
}

public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffValidator() => RuleFor(c => c.Staff).NotNull().SetValidator(new StaffFieldsValidator());
}

public sealed class CreateRoomAssignmentValidator : AbstractValidator<CreateRoomAssignmentCommand>
{
    public CreateRoomAssignmentValidator()
    {
        RuleFor(c => c.DayOfWeek).InclusiveBetween(0, 6);
        RuleFor(c => c.EndTime).GreaterThan(c => c.StartTime).WithMessage("End time must be after start time.");
        RuleFor(c => c.EffectiveTo)
            .Must((c, to) => to is null || to >= c.EffectiveFrom).WithMessage("End of assignment cannot be before its start.");
    }
}

internal static class StaffMapping
{
    public static StaffDetailDto ToDetail(Staff s, bool includeSensitive) => new(
        s.Id, s.FirstName, s.LastName, s.DisplayName, s.DateOfBirth, s.Gender, s.Email, s.Phone, s.JobTitle,
        s.HireDate, s.EndDate, s.Status, s.Barcode,
        includeSensitive ? s.EmiratesId : null,
        includeSensitive ? s.PassportNumber : null,
        includeSensitive && long.TryParse(s.SalaryAmountFils, out var fils) ? fils : null,
        s.CreatedAt);

    public static void Apply(Staff s, StaffFields f)
    {
        s.FirstName = f.FirstName.Trim();
        s.LastName = f.LastName.Trim();
        s.DateOfBirth = f.DateOfBirth;
        s.Gender = f.Gender;
        s.Email = f.Email.Trim().ToLowerInvariant();
        s.Phone = f.Phone;
        s.JobTitle = f.JobTitle;
        s.HireDate = f.HireDate;
        s.EndDate = f.EndDate;
        s.Status = f.Status;
        s.EmiratesId = f.EmiratesId;
        s.PassportNumber = f.PassportNumber;
        s.SalaryAmountFils = f.SalaryAmountFils?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Teachers only see colleagues who work in the rooms they are assigned to, plus themselves.</summary>
    public static async Task<IQueryable<Staff>> ScopeAsync(
        this IQueryable<Staff> staff, IAppDbContext db, IAccessScope scope, ICurrentUser user, CancellationToken ct)
    {
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is null)
        {
            return staff;
        }

        var userId = user.UserId;
        return staff.Where(s => s.AppUserId == userId
            || db.StaffRoomAssignments.Any(a => a.StaffId == s.Id && rooms.Contains(a.RoomId)));
    }
}

public sealed class ListStaffHandler(IAppDbContext db, IAccessScope scope, ICurrentUser user)
    : IRequestHandler<ListStaffQuery, Result<CursorPage<StaffSummaryDto>>>
{
    public async Task<Result<CursorPage<StaffSummaryDto>>> Handle(ListStaffQuery request, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().ScopeAsync(db, scope, user, ct);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            staff = staff.Where(s => s.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            staff = staff.Where(s =>
                s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term) || s.Email.Contains(term));
        }

        var page = await staff.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            s => new StaffSummaryDto(
                s.Id, s.FirstName, s.LastName, s.Email, s.Phone, s.JobTitle, s.Status, s.HireDate, s.Barcode, s.CreatedAt),
            ct);
        return Result<CursorPage<StaffSummaryDto>>.Success(page);
    }
}

public sealed class GetStaffHandler(IAppDbContext db, IAccessScope scope, ICurrentUser user)
    : IRequestHandler<GetStaffQuery, Result<StaffDetailDto>>
{
    public async Task<Result<StaffDetailDto>> Handle(GetStaffQuery request, CancellationToken ct)
    {
        var scoped = await db.Staff.AsNoTracking().ScopeAsync(db, scope, user, ct);
        var staff = await scoped.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        return staff is null
            ? Error.NotFound("Staff member not found.")
            : Result<StaffDetailDto>.Success(StaffMapping.ToDetail(staff, user.HasPermission(Permissions.StaffWrite)));
    }
}

public sealed class CreateStaffHandler(IAppDbContext db, ITenantContext tenant)
    : IRequestHandler<CreateStaffCommand, Result<StaffDetailDto>>
{
    public async Task<Result<StaffDetailDto>> Handle(CreateStaffCommand request, CancellationToken ct)
    {
        var email = request.Staff.Email.Trim().ToLowerInvariant();
        if (await db.Staff.AnyAsync(s => s.Email == email, ct))
        {
            return Error.Conflict("A staff member with this email already exists.", "email");
        }

        var staff = new Staff { Barcode = await NextBarcodeAsync(ct) };
        StaffMapping.Apply(staff, request.Staff);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);
        return Result<StaffDetailDto>.Success(StaffMapping.ToDetail(staff, includeSensitive: true));
    }

    private async Task<string> NextBarcodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var candidate = BarcodeGenerator.Next("STF");
            var taken = await db.Staff.IgnoreQueryFilters()
                .AnyAsync(s => s.TenantId == tenant.TenantId && s.Barcode == candidate, ct);
            if (!taken)
            {
                return candidate;
            }
        }
    }
}

public sealed class UpdateStaffHandler(IAppDbContext db) : IRequestHandler<UpdateStaffCommand, Result<StaffDetailDto>>
{
    public async Task<Result<StaffDetailDto>> Handle(UpdateStaffCommand request, CancellationToken ct)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (staff is null)
        {
            return Error.NotFound("Staff member not found.");
        }

        var email = request.Staff.Email.Trim().ToLowerInvariant();
        if (await db.Staff.AnyAsync(s => s.Id != request.Id && s.Email == email, ct))
        {
            return Error.Conflict("A staff member with this email already exists.", "email");
        }

        StaffMapping.Apply(staff, request.Staff);
        await db.SaveChangesAsync(ct);
        return Result<StaffDetailDto>.Success(StaffMapping.ToDetail(staff, includeSensitive: true));
    }
}

public sealed class DeleteStaffHandler(IAppDbContext db) : IRequestHandler<DeleteStaffCommand, Result>
{
    public async Task<Result> Handle(DeleteStaffCommand request, CancellationToken ct)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (staff is null)
        {
            return Result.Failure(Error.NotFound("Staff member not found."));
        }

        db.Staff.Remove(staff);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ListRoomAssignmentsHandler(IAppDbContext db)
    : IRequestHandler<ListRoomAssignmentsQuery, Result<IReadOnlyList<RoomAssignmentDto>>>
{
    public async Task<Result<IReadOnlyList<RoomAssignmentDto>>> Handle(ListRoomAssignmentsQuery request, CancellationToken ct)
    {
        if (!await db.Staff.AnyAsync(s => s.Id == request.StaffId, ct))
        {
            return Error.NotFound("Staff member not found.");
        }

        var items = await db.StaffRoomAssignments.AsNoTracking()
            .Where(a => a.StaffId == request.StaffId)
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => new RoomAssignmentDto(
                a.Id, a.StaffId, a.RoomId, a.DayOfWeek, a.StartTime, a.EndTime, a.EffectiveFrom, a.EffectiveTo, a.CreatedAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<RoomAssignmentDto>>.Success(items);
    }
}

public sealed class CreateRoomAssignmentHandler(IAppDbContext db)
    : IRequestHandler<CreateRoomAssignmentCommand, Result<RoomAssignmentDto>>
{
    public async Task<Result<RoomAssignmentDto>> Handle(CreateRoomAssignmentCommand request, CancellationToken ct)
    {
        if (!await db.Staff.AnyAsync(s => s.Id == request.StaffId, ct))
        {
            return Error.NotFound("Staff member not found.");
        }

        if (!await db.Rooms.AnyAsync(r => r.Id == request.RoomId, ct))
        {
            return Error.Validation("The selected room does not exist.", "room_id");
        }

        var assignment = new StaffRoomAssignment
        {
            StaffId = request.StaffId,
            RoomId = request.RoomId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
        };
        db.StaffRoomAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return Result<RoomAssignmentDto>.Success(new RoomAssignmentDto(
            assignment.Id, assignment.StaffId, assignment.RoomId, assignment.DayOfWeek, assignment.StartTime,
            assignment.EndTime, assignment.EffectiveFrom, assignment.EffectiveTo, assignment.CreatedAt));
    }
}
