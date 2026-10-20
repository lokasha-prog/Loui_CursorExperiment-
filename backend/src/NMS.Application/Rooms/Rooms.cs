using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Rooms;

public sealed record RoomDto(
    Guid Id,
    Dictionary<string, string> Name,
    int Capacity,
    int MinAgeMonths,
    int MaxAgeMonths,
    int RatioStaffToStudent,
    bool IsActive,
    int StudentCount,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListRoomsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<RoomDto>>>;

public sealed record GetRoomQuery(Guid Id) : IRequest<Result<RoomDto>>;

public sealed record CreateRoomCommand(
    Dictionary<string, string> Name,
    int Capacity,
    int MinAgeMonths,
    int MaxAgeMonths,
    int RatioStaffToStudent,
    bool IsActive = true) : IRequest<Result<RoomDto>>;

public sealed record UpdateRoomCommand(
    Guid Id,
    Dictionary<string, string> Name,
    int Capacity,
    int MinAgeMonths,
    int MaxAgeMonths,
    int RatioStaffToStudent,
    bool IsActive) : IRequest<Result<RoomDto>>;

public sealed record DeleteRoomCommand(Guid Id) : IRequest<Result>;

public abstract class RoomRules<T> : AbstractValidator<T>
    where T : notnull
{
    protected void Apply(
        System.Linq.Expressions.Expression<Func<T, Dictionary<string, string>>> name,
        System.Linq.Expressions.Expression<Func<T, int>> capacity,
        System.Linq.Expressions.Expression<Func<T, int>> minAge,
        System.Linq.Expressions.Expression<Func<T, int>> maxAge,
        System.Linq.Expressions.Expression<Func<T, int>> ratio,
        Func<T, int> minAgeValue)
    {
        RuleFor(name).MustHaveEnglish();
        RuleFor(capacity).InclusiveBetween(1, 500);
        RuleFor(minAge).GreaterThanOrEqualTo(0);
        RuleFor(maxAge).GreaterThanOrEqualTo(x => minAgeValue(x))
            .WithMessage("Maximum age must not be lower than minimum age.");
        RuleFor(ratio).InclusiveBetween(1, 50);
    }
}

public sealed class CreateRoomValidator : RoomRules<CreateRoomCommand>
{
    public CreateRoomValidator() =>
        Apply(c => c.Name, c => c.Capacity, c => c.MinAgeMonths, c => c.MaxAgeMonths, c => c.RatioStaffToStudent, c => c.MinAgeMonths);
}

public sealed class UpdateRoomValidator : RoomRules<UpdateRoomCommand>
{
    public UpdateRoomValidator() =>
        Apply(c => c.Name, c => c.Capacity, c => c.MinAgeMonths, c => c.MaxAgeMonths, c => c.RatioStaffToStudent, c => c.MinAgeMonths);
}

internal static class RoomQueries
{
    public static IQueryable<RoomDto> Project(IAppDbContext db, IQueryable<Room> rooms) =>
        rooms.Select(r => new RoomDto(
            r.Id,
            r.Name,
            r.Capacity,
            r.MinAgeMonths,
            r.MaxAgeMonths,
            r.RatioStaffToStudent,
            r.IsActive,
            db.Students.Count(s => s.RoomId == r.Id),
            r.CreatedAt));

    public static async Task<bool> NameExistsAsync(
        IAppDbContext db, string english, Guid? exceptId, CancellationToken ct)
    {
        var names = await db.Rooms.AsNoTracking().Where(r => r.Id != exceptId).Select(r => r.Name).ToListAsync(ct);
        return NameConflict.Exists(names, english);
    }
}

public sealed class ListRoomsHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<ListRoomsQuery, Result<CursorPage<RoomDto>>>
{
    public async Task<Result<CursorPage<RoomDto>>> Handle(ListRoomsQuery request, CancellationToken ct)
    {
        var visible = await scope.GetVisibleRoomIdsAsync(ct);
        var rooms = db.Rooms.AsNoTracking();
        if (visible is not null)
        {
            rooms = rooms.Where(r => visible.Contains(r.Id));
        }

        var page = await rooms.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            r => new RoomDto(
                r.Id, r.Name, r.Capacity, r.MinAgeMonths, r.MaxAgeMonths, r.RatioStaffToStudent, r.IsActive,
                db.Students.Count(s => s.RoomId == r.Id), r.CreatedAt),
            ct);
        return Result<CursorPage<RoomDto>>.Success(page);
    }
}

public sealed class GetRoomHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetRoomQuery, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(GetRoomQuery request, CancellationToken ct)
    {
        var visible = await scope.GetVisibleRoomIdsAsync(ct);
        if (visible is not null && !visible.Contains(request.Id))
        {
            return Error.NotFound("Room not found.");
        }

        var room = await RoomQueries.Project(db, db.Rooms.AsNoTracking().Where(r => r.Id == request.Id))
            .FirstOrDefaultAsync(ct);
        return room is null ? Error.NotFound("Room not found.") : Result<RoomDto>.Success(room);
    }
}

public sealed class CreateRoomHandler(IAppDbContext db) : IRequestHandler<CreateRoomCommand, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(CreateRoomCommand request, CancellationToken ct)
    {
        if (await RoomQueries.NameExistsAsync(db, request.Name["en"], null, ct))
        {
            return Error.Conflict("A room with this name already exists.", "name");
        }

        var room = new Room
        {
            Name = request.Name,
            Capacity = request.Capacity,
            MinAgeMonths = request.MinAgeMonths,
            MaxAgeMonths = request.MaxAgeMonths,
            RatioStaffToStudent = request.RatioStaffToStudent,
            IsActive = request.IsActive,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);

        return Result<RoomDto>.Success(new RoomDto(
            room.Id, room.Name, room.Capacity, room.MinAgeMonths, room.MaxAgeMonths,
            room.RatioStaffToStudent, room.IsActive, 0, room.CreatedAt));
    }
}

public sealed class UpdateRoomHandler(IAppDbContext db) : IRequestHandler<UpdateRoomCommand, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(UpdateRoomCommand request, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (room is null)
        {
            return Error.NotFound("Room not found.");
        }

        if (await RoomQueries.NameExistsAsync(db, request.Name["en"], request.Id, ct))
        {
            return Error.Conflict("A room with this name already exists.", "name");
        }

        room.Name = request.Name;
        room.Capacity = request.Capacity;
        room.MinAgeMonths = request.MinAgeMonths;
        room.MaxAgeMonths = request.MaxAgeMonths;
        room.RatioStaffToStudent = request.RatioStaffToStudent;
        room.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        var studentCount = await db.Students.CountAsync(s => s.RoomId == room.Id, ct);
        return Result<RoomDto>.Success(new RoomDto(
            room.Id, room.Name, room.Capacity, room.MinAgeMonths, room.MaxAgeMonths,
            room.RatioStaffToStudent, room.IsActive, studentCount, room.CreatedAt));
    }
}

public sealed class DeleteRoomHandler(IAppDbContext db) : IRequestHandler<DeleteRoomCommand, Result>
{
    public async Task<Result> Handle(DeleteRoomCommand request, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (room is null)
        {
            return Result.Failure(Error.NotFound("Room not found."));
        }

        if (await db.Students.AnyAsync(s => s.RoomId == room.Id, ct))
        {
            return Result.Failure(Error.InvalidState("Move the students out of this room before deleting it."));
        }

        db.Rooms.Remove(room);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
