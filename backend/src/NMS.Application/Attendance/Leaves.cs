using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Attendance;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed record LeaveTypeDto(
    Guid Id,
    Dictionary<string, string> Name,
    string AppliesTo,
    bool RequiresApproval,
    bool AffectsAttendance,
    bool AffectsRatio,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListLeaveTypesQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<LeaveTypeDto>>>;

public sealed record CreateLeaveTypeCommand(
    Dictionary<string, string> Name, string AppliesTo, bool RequiresApproval = true, bool AffectsAttendance = true, bool AffectsRatio = false)
    : IRequest<Result<LeaveTypeDto>>;

public sealed record UpdateLeaveTypeCommand(
    Guid Id, Dictionary<string, string> Name, string AppliesTo, bool RequiresApproval, bool AffectsAttendance, bool AffectsRatio)
    : IRequest<Result<LeaveTypeDto>>;

public sealed record DeleteLeaveTypeCommand(Guid Id) : IRequest<Result>;

public sealed record LeaveRequestDto(
    Guid Id,
    string PersonType,
    Guid PersonId,
    string? PersonName,
    Guid LeaveTypeId,
    Dictionary<string, string> LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string Status,
    DateTimeOffset? ApprovedAt,
    string? ApprovalNotes,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListLeavesQuery(string? Status, string? PersonType, string? Cursor, int? Limit)
    : IRequest<Result<CursorPage<LeaveRequestDto>>>;

public sealed record GetLeaveQuery(Guid Id) : IRequest<Result<LeaveRequestDto>>;

public sealed record CreateLeaveCommand(
    string PersonType, Guid PersonId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason)
    : IRequest<Result<LeaveRequestDto>>;

public sealed record DecideLeaveCommand(Guid Id, bool Approve, string? Notes) : IRequest<Result<LeaveRequestDto>>;

public sealed record CancelLeaveCommand(Guid Id) : IRequest<Result<LeaveRequestDto>>;

public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.AppliesTo).Must(a => LeaveAppliesTo.All.Contains(a)).WithMessage("Applies to must be student, staff or both.");
    }
}

public sealed class UpdateLeaveTypeValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    public UpdateLeaveTypeValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.AppliesTo).Must(a => LeaveAppliesTo.All.Contains(a)).WithMessage("Applies to must be student, staff or both.");
    }
}

public sealed class CreateLeaveValidator : AbstractValidator<CreateLeaveCommand>
{
    public CreateLeaveValidator()
    {
        RuleFor(c => c.PersonType).Must(t => PersonTypes.All.Contains(t)).WithMessage("Person type must be student or staff.");
        RuleFor(c => c.EndDate).GreaterThanOrEqualTo(c => c.StartDate).WithMessage("The end date must not be before the start date.");
        RuleFor(c => c.Reason!).MaximumLength(1000).When(c => c.Reason is not null);
    }
}

internal static class LeaveMapping
{
    public static LeaveRequestDto ToDto(LeaveRequest l, string? personName) => new(
        l.Id, l.PersonType, l.PersonId, personName, l.LeaveTypeId, l.LeaveType!.Name, l.StartDate, l.EndDate, l.Reason,
        l.Status, l.ApprovedAt, l.ApprovalNotes, l.CreatedAt);

    public static LeaveTypeDto ToDto(LeaveType t) => new(
        t.Id, t.Name, t.AppliesTo, t.RequiresApproval, t.AffectsAttendance, t.AffectsRatio, t.CreatedAt);

    /// <summary>Teachers see leave for children in their rooms; parents for their own children; staff leave needs full access.</summary>
    public static async Task<IQueryable<LeaveRequest>> ScopeAsync(IQueryable<LeaveRequest> leaves, IAppDbContext db, IAccessScope scope, CancellationToken ct)
    {
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            leaves = leaves.Where(l => l.PersonType == PersonTypes.Student && own.Contains(l.PersonId));
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            leaves = leaves.Where(l => l.PersonType == PersonTypes.Student
                && db.Students.Any(s => s.Id == l.PersonId && s.RoomId != null && rooms.Contains(s.RoomId.Value)));
        }

        return leaves;
    }

    public static Task<string?> NameAsync(IAppDbContext db, string type, Guid id, CancellationToken ct) =>
        type == PersonTypes.Student
            ? db.Students.AsNoTracking().Where(s => s.Id == id).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct)
            : db.Staff.AsNoTracking().Where(s => s.Id == id).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct);
}

public sealed class ListLeaveTypesHandler(IAppDbContext db) : IRequestHandler<ListLeaveTypesQuery, Result<CursorPage<LeaveTypeDto>>>
{
    public async Task<Result<CursorPage<LeaveTypeDto>>> Handle(ListLeaveTypesQuery request, CancellationToken ct) =>
        Result<CursorPage<LeaveTypeDto>>.Success(await db.LeaveTypes.AsNoTracking().ToCursorPageAsync(
            request.Cursor, request.Limit,
            t => new LeaveTypeDto(t.Id, t.Name, t.AppliesTo, t.RequiresApproval, t.AffectsAttendance, t.AffectsRatio, t.CreatedAt), ct));
}

public sealed class CreateLeaveTypeHandler(IAppDbContext db) : IRequestHandler<CreateLeaveTypeCommand, Result<LeaveTypeDto>>
{
    public async Task<Result<LeaveTypeDto>> Handle(CreateLeaveTypeCommand request, CancellationToken ct)
    {
        var existing = await db.LeaveTypes.AsNoTracking().Select(t => t.Name).ToListAsync(ct);
        if (NameConflict.Exists(existing, request.Name["en"]))
        {
            return Error.Conflict("A leave type with this name already exists.", "name");
        }

        var type = new LeaveType
        {
            Name = request.Name, AppliesTo = request.AppliesTo, RequiresApproval = request.RequiresApproval,
            AffectsAttendance = request.AffectsAttendance, AffectsRatio = request.AffectsRatio,
        };
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return Result<LeaveTypeDto>.Success(LeaveMapping.ToDto(type));
    }
}

public sealed class UpdateLeaveTypeHandler(IAppDbContext db) : IRequestHandler<UpdateLeaveTypeCommand, Result<LeaveTypeDto>>
{
    public async Task<Result<LeaveTypeDto>> Handle(UpdateLeaveTypeCommand request, CancellationToken ct)
    {
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (type is null)
        {
            return Error.NotFound("Leave type not found.");
        }

        var others = await db.LeaveTypes.AsNoTracking().Where(t => t.Id != request.Id).Select(t => t.Name).ToListAsync(ct);
        if (NameConflict.Exists(others, request.Name["en"]))
        {
            return Error.Conflict("A leave type with this name already exists.", "name");
        }

        type.Name = request.Name;
        type.AppliesTo = request.AppliesTo;
        type.RequiresApproval = request.RequiresApproval;
        type.AffectsAttendance = request.AffectsAttendance;
        type.AffectsRatio = request.AffectsRatio;
        await db.SaveChangesAsync(ct);
        return Result<LeaveTypeDto>.Success(LeaveMapping.ToDto(type));
    }
}

public sealed class DeleteLeaveTypeHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteLeaveTypeCommand, Result>
{
    public async Task<Result> Handle(DeleteLeaveTypeCommand request, CancellationToken ct)
    {
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (type is null)
        {
            return Result.Failure(Error.NotFound("Leave type not found."));
        }

        if (await db.LeaveRequests.AnyAsync(l => l.LeaveTypeId == type.Id, ct))
        {
            return Result.Failure(Error.Conflict("This leave type is used by existing requests and cannot be removed."));
        }

        type.IsDeleted = true;
        type.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ListLeavesHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<ListLeavesQuery, Result<CursorPage<LeaveRequestDto>>>
{
    public async Task<Result<CursorPage<LeaveRequestDto>>> Handle(ListLeavesQuery request, CancellationToken ct)
    {
        var leaves = db.LeaveRequests.AsNoTracking();
        if (request.Status is { } status)
        {
            leaves = leaves.Where(l => l.Status == status);
        }

        if (request.PersonType is { } type)
        {
            leaves = leaves.Where(l => l.PersonType == type);
        }

        leaves = await LeaveMapping.ScopeAsync(leaves, db, scope, ct);
        var page = await leaves.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            l => new LeaveRequestDto(
                l.Id, l.PersonType, l.PersonId,
                l.PersonType == PersonTypes.Student
                    ? db.Students.Where(s => s.Id == l.PersonId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault()
                    : db.Staff.Where(s => s.Id == l.PersonId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
                l.LeaveTypeId, l.LeaveType!.Name, l.StartDate, l.EndDate, l.Reason, l.Status, l.ApprovedAt, l.ApprovalNotes, l.CreatedAt),
            ct);
        return Result<CursorPage<LeaveRequestDto>>.Success(page);
    }
}

public sealed class GetLeaveHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetLeaveQuery, Result<LeaveRequestDto>>
{
    public async Task<Result<LeaveRequestDto>> Handle(GetLeaveQuery request, CancellationToken ct)
    {
        var leave = await (await LeaveMapping.ScopeAsync(db.LeaveRequests.AsNoTracking().Include(l => l.LeaveType), db, scope, ct))
            .FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        return leave is null
            ? Error.NotFound("Leave request not found.")
            : Result<LeaveRequestDto>.Success(LeaveMapping.ToDto(leave, await LeaveMapping.NameAsync(db, leave.PersonType, leave.PersonId, ct)));
    }
}

public sealed class CreateLeaveHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, IClock clock)
    : IRequestHandler<CreateLeaveCommand, Result<LeaveRequestDto>>
{
    public async Task<Result<LeaveRequestDto>> Handle(CreateLeaveCommand request, CancellationToken ct)
    {
        var person = await PersonResolver.ResolveAsync(db, scope, null, request.PersonType, request.PersonId, ct);
        if (person is null)
        {
            return Error.NotFound("Person not found.");
        }

        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId, ct);
        if (type is null)
        {
            return Error.Validation("The selected leave type does not exist.", "leave_type_id");
        }

        if (type.AppliesTo != LeaveAppliesTo.Both && type.AppliesTo != person.Type)
        {
            return Error.Validation("This leave type does not apply to this kind of person.", "leave_type_id");
        }

        var overlaps = await db.LeaveRequests.AnyAsync(
            l => l.PersonType == person.Type && l.PersonId == person.Id
                && (l.Status == LeaveStatuses.Submitted || l.Status == LeaveStatuses.Approved)
                && l.StartDate <= request.EndDate && l.EndDate >= request.StartDate, ct);
        if (overlaps)
        {
            return Error.Conflict("This person already has a leave request for some of these dates.");
        }

        var leave = new LeaveRequest
        {
            PersonType = person.Type,
            PersonId = person.Id,
            LeaveTypeId = type.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason?.Trim(),
        };
        if (!type.RequiresApproval)
        {
            leave.Status = LeaveStatuses.Approved;
            leave.ApprovedBy = current.UserId;
            leave.ApprovedAt = clock.UtcNow;
        }

        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(ct);
        leave.LeaveType = type;
        return Result<LeaveRequestDto>.Success(LeaveMapping.ToDto(leave, person.Name));
    }
}

public sealed class DecideLeaveHandler(IAppDbContext db, ICurrentUser current, IClock clock)
    : IRequestHandler<DecideLeaveCommand, Result<LeaveRequestDto>>
{
    public async Task<Result<LeaveRequestDto>> Handle(DecideLeaveCommand request, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.Include(l => l.LeaveType).FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (leave is null)
        {
            return Error.NotFound("Leave request not found.");
        }

        if (leave.Status != LeaveStatuses.Submitted)
        {
            return Error.InvalidState("Only a submitted request can be approved or rejected.");
        }

        leave.Status = request.Approve ? LeaveStatuses.Approved : LeaveStatuses.Rejected;
        leave.ApprovedBy = current.UserId;
        leave.ApprovedAt = clock.UtcNow;
        leave.ApprovalNotes = request.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<LeaveRequestDto>.Success(LeaveMapping.ToDto(leave, await LeaveMapping.NameAsync(db, leave.PersonType, leave.PersonId, ct)));
    }
}

public sealed class CancelLeaveHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<CancelLeaveCommand, Result<LeaveRequestDto>>
{
    public async Task<Result<LeaveRequestDto>> Handle(CancelLeaveCommand request, CancellationToken ct)
    {
        var leave = await (await LeaveMapping.ScopeAsync(db.LeaveRequests.Include(l => l.LeaveType), db, scope, ct))
            .FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (leave is null)
        {
            return Error.NotFound("Leave request not found.");
        }

        if (leave.Status is not (LeaveStatuses.Submitted or LeaveStatuses.Approved))
        {
            return Error.InvalidState("This request can no longer be cancelled.");
        }

        leave.Status = LeaveStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
        return Result<LeaveRequestDto>.Success(LeaveMapping.ToDto(leave, await LeaveMapping.NameAsync(db, leave.PersonType, leave.PersonId, ct)));
    }
}
