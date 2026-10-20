using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Groups;

public sealed record GroupDto(Guid Id, Dictionary<string, string> Name, Guid RoomId, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListGroupsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<GroupDto>>>;

public sealed record CreateGroupCommand(Dictionary<string, string> Name, Guid RoomId) : IRequest<Result<GroupDto>>;

public sealed record UpdateGroupCommand(Guid Id, Dictionary<string, string> Name, Guid RoomId) : IRequest<Result<GroupDto>>;

public sealed record DeleteGroupCommand(Guid Id) : IRequest<Result>;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.RoomId).NotEmpty();
    }
}

public sealed class UpdateGroupValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.RoomId).NotEmpty();
    }
}

public sealed class ListGroupsHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<ListGroupsQuery, Result<CursorPage<GroupDto>>>
{
    public async Task<Result<CursorPage<GroupDto>>> Handle(ListGroupsQuery request, CancellationToken ct)
    {
        var visible = await scope.GetVisibleRoomIdsAsync(ct);
        var groups = db.StudentGroups.AsNoTracking();
        if (visible is not null)
        {
            groups = groups.Where(g => visible.Contains(g.RoomId));
        }

        var page = await groups.ToCursorPageAsync(
            request.Cursor, request.Limit, g => new GroupDto(g.Id, g.Name, g.RoomId, g.CreatedAt), ct);
        return Result<CursorPage<GroupDto>>.Success(page);
    }
}

public sealed class CreateGroupHandler(IAppDbContext db) : IRequestHandler<CreateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand request, CancellationToken ct)
    {
        if (!await db.Rooms.AnyAsync(r => r.Id == request.RoomId, ct))
        {
            return Error.Validation("The selected room does not exist.", "room_id");
        }

        var group = new StudentGroup { Name = request.Name, RoomId = request.RoomId };
        db.StudentGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return Result<GroupDto>.Success(new GroupDto(group.Id, group.Name, group.RoomId, group.CreatedAt));
    }
}

public sealed class UpdateGroupHandler(IAppDbContext db) : IRequestHandler<UpdateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(UpdateGroupCommand request, CancellationToken ct)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, ct);
        if (group is null)
        {
            return Error.NotFound("Group not found.");
        }

        if (!await db.Rooms.AnyAsync(r => r.Id == request.RoomId, ct))
        {
            return Error.Validation("The selected room does not exist.", "room_id");
        }

        group.Name = request.Name;
        group.RoomId = request.RoomId;
        await db.SaveChangesAsync(ct);
        return Result<GroupDto>.Success(new GroupDto(group.Id, group.Name, group.RoomId, group.CreatedAt));
    }
}

public sealed class DeleteGroupHandler(IAppDbContext db) : IRequestHandler<DeleteGroupCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupCommand request, CancellationToken ct)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, ct);
        if (group is null)
        {
            return Result.Failure(Error.NotFound("Group not found."));
        }

        if (await db.Students.AnyAsync(s => s.GroupId == group.Id, ct))
        {
            return Result.Failure(Error.InvalidState("Move the students out of this group before deleting it."));
        }

        db.StudentGroups.Remove(group);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
