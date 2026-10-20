using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Security;
using NMS.Domain.CentreManagement;
using NMS.Domain.Communication;
using NMS.Domain.Identity;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Communication;

public sealed record ThreadDto(
    Guid Id,
    string Subject,
    Guid? StudentId,
    bool IsBroadcast,
    string? LastMessagePreview,
    string? LastSenderName,
    DateTimeOffset LastMessageAt,
    bool Unread,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record MessageDto(Guid Id, Guid SenderUserId, string? SenderName, bool FromStaff, string Body, DateTimeOffset CreatedAt);

public sealed record ThreadDetailDto(Guid Id, string Subject, Guid? StudentId, bool IsBroadcast, bool CanReply, IReadOnlyList<MessageDto> Messages);

public sealed record ListThreadsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<ThreadDto>>>;

public sealed record GetThreadQuery(Guid Id) : IRequest<Result<ThreadDetailDto>>;

/// <summary>Families write to their centre about a child; staff write to a child's family or broadcast to many families.</summary>
public sealed record CreateThreadCommand(string Subject, string Body, Guid? StudentId, bool Broadcast, Guid? RoomId) : IRequest<Result<ThreadDetailDto>>;

public sealed record ReplyCommand(Guid ThreadId, string Body) : IRequest<Result<MessageDto>>;

/// <summary>Raised for every new message so connected screens of the other participants can refresh.</summary>
public sealed record MessageSent(Guid TenantId, Guid ThreadId, IReadOnlyList<Guid> RecipientUserIds, bool ToStaffInbox) : INotification;

public sealed class CreateThreadValidator : AbstractValidator<CreateThreadCommand>
{
    public CreateThreadValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ReplyValidator : AbstractValidator<ReplyCommand>
{
    public ReplyValidator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(4000);
}

internal static class Inbox
{
    public static bool IsFamily(ICurrentUser current) => current.IsInRole(SystemRoles.Parent) && current.Roles.Count == 1;

    public static bool IsInboxStaff(ICurrentUser current) => !IsFamily(current) && current.HasPermission(Permissions.MessagingRead);

    /// <summary>Threads the person can see: their own, plus (for staff who handle messages) every thread a family started.</summary>
    public static IQueryable<MessageThread> Visible(IAppDbContext db, ICurrentUser current)
    {
        var me = current.UserId;
        var threads = db.MessageThreads.AsNoTracking();
        return IsInboxStaff(current)
            ? threads.Where(t => t.OpenToStaff || db.ThreadParticipants.Any(p => p.ThreadId == t.Id && p.UserId == me))
            : threads.Where(t => db.ThreadParticipants.Any(p => p.ThreadId == t.Id && p.UserId == me));
    }
}

public sealed class ListThreadsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListThreadsQuery, Result<CursorPage<ThreadDto>>>
{
    public async Task<Result<CursorPage<ThreadDto>>> Handle(ListThreadsQuery request, CancellationToken ct)
    {
        var me = current.UserId;
        var page = await Inbox.Visible(db, current).ToCursorPageAsync(request.Cursor, request.Limit,
            t => new ThreadDto(
                t.Id, t.Subject, t.StudentId, t.IsBroadcast,
                db.Messages.Where(m => m.ThreadId == t.Id).OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault(),
                db.Messages.Where(m => m.ThreadId == t.Id).OrderByDescending(m => m.CreatedAt)
                    .Select(m => db.AppUsers.Where(u => u.Id == m.SenderUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()).FirstOrDefault(),
                t.LastMessageAt,
                db.Messages.Any(m => m.ThreadId == t.Id && m.SenderUserId != me
                    && !db.ThreadParticipants.Any(p => p.ThreadId == t.Id && p.UserId == me && p.LastReadAt != null && p.LastReadAt >= m.CreatedAt)),
                t.CreatedAt),
            ct);
        return Result<CursorPage<ThreadDto>>.Success(page);
    }
}

public sealed class GetThreadHandler(IAppDbContext db, ICurrentUser current, IClock clock) : IRequestHandler<GetThreadQuery, Result<ThreadDetailDto>>
{
    public async Task<Result<ThreadDetailDto>> Handle(GetThreadQuery request, CancellationToken ct)
    {
        var thread = await Inbox.Visible(db, current).FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (thread is null)
        {
            return Error.NotFound("Conversation not found.");
        }

        var staffIds = await StaffUserIdsAsync(db, ct);
        var messages = await db.Messages.AsNoTracking().Where(m => m.ThreadId == thread.Id).OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.SenderUserId, m.Body, m.CreatedAt, Name = db.AppUsers.Where(u => u.Id == m.SenderUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() })
            .ToListAsync(ct);

        // Opening a conversation marks it read (this is the read receipt).
        var mine = await db.ThreadParticipants.FirstOrDefaultAsync(p => p.ThreadId == thread.Id && p.UserId == current.UserId, ct);
        if (mine is null && Inbox.IsInboxStaff(current))
        {
            db.ThreadParticipants.Add(new ThreadParticipant { ThreadId = thread.Id, UserId = current.UserId, LastReadAt = clock.UtcNow });
        }
        else if (mine is not null)
        {
            mine.LastReadAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        var canReply = !thread.IsBroadcast || Inbox.IsInboxStaff(current);
        return Result<ThreadDetailDto>.Success(new ThreadDetailDto(
            thread.Id, thread.Subject, thread.StudentId, thread.IsBroadcast, canReply,
            messages.Select(m => new MessageDto(m.Id, m.SenderUserId, m.Name, staffIds.Contains(m.SenderUserId), m.Body, m.CreatedAt)).ToList()));
    }

    internal static async Task<HashSet<Guid>> StaffUserIdsAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.Staff.AsNoTracking().Where(s => s.AppUserId != null).Select(s => s.AppUserId!.Value).ToListAsync(ct)).ToHashSet();
}

public sealed class CreateThreadHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, ITenantContext tenant, IClock clock, IPublisher publisher)
    : IRequestHandler<CreateThreadCommand, Result<ThreadDetailDto>>
{
    public async Task<Result<ThreadDetailDto>> Handle(CreateThreadCommand request, CancellationToken ct)
    {
        var family = Inbox.IsFamily(current);
        var recipients = new List<Guid>();
        Guid? studentId = request.StudentId;

        if (family)
        {
            if (request.Broadcast)
            {
                return Error.Forbidden("Families cannot send broadcasts.");
            }

            var own = await scope.GetOwnStudentIdsAsync(ct) ?? [];
            if (studentId is not null && !own.Contains(studentId.Value))
            {
                return Error.NotFound("Child not found.");
            }
        }
        else if (request.Broadcast)
        {
            if (!current.HasPermission(Permissions.MessagingBroadcast))
            {
                return Error.Forbidden("You cannot send broadcasts.");
            }

            var students = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
            if (request.RoomId is { } roomId)
            {
                students = students.Where(s => s.RoomId == roomId);
            }

            var ids = await students.Select(s => s.Id).ToListAsync(ct);
            recipients = await FamilyUsersAsync(db, ids, ct);
            studentId = null;
        }
        else
        {
            if (studentId is null)
            {
                return Error.Validation("Choose the child this message is about.", "student_id");
            }

            var visible = await scope.GetVisibleRoomIdsAsync(ct);
            var child = db.Students.AsNoTracking().Where(s => s.Id == studentId);
            if (visible is not null)
            {
                child = child.Where(s => s.RoomId != null && visible.Contains(s.RoomId.Value));
            }

            if (!await child.AnyAsync(ct))
            {
                return Error.NotFound("Child not found.");
            }

            recipients = await FamilyUsersAsync(db, [studentId.Value], ct);
            if (recipients.Count == 0)
            {
                return Error.InvalidState("This child's family has no account yet, so they cannot be messaged.");
            }
        }

        var now = clock.UtcNow;
        var thread = new MessageThread
        {
            Subject = request.Subject.Trim(), CreatedByUserId = current.UserId, StudentId = studentId,
            IsBroadcast = request.Broadcast, OpenToStaff = family, LastMessageAt = now,
        };
        db.MessageThreads.Add(thread);
        db.ThreadParticipants.Add(new ThreadParticipant { ThreadId = thread.Id, UserId = current.UserId, LastReadAt = now });
        foreach (var id in recipients.Where(r => r != current.UserId))
        {
            db.ThreadParticipants.Add(new ThreadParticipant { ThreadId = thread.Id, UserId = id });
        }

        var message = new Message { ThreadId = thread.Id, SenderUserId = current.UserId, Body = request.Body.Trim() };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new MessageSent(tenant.TenantId, thread.Id, recipients, family), ct);
        return await new GetThreadHandler(db, current, clock).Handle(new GetThreadQuery(thread.Id), ct);
    }

    private static async Task<List<Guid>> FamilyUsersAsync(IAppDbContext db, IReadOnlyList<Guid> studentIds, CancellationToken ct) =>
        await db.StudentGuardians.AsNoTracking()
            .Where(sg => studentIds.Contains(sg.StudentId) && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);
}

public sealed class ReplyHandler(IAppDbContext db, ICurrentUser current, ITenantContext tenant, IClock clock, IPublisher publisher)
    : IRequestHandler<ReplyCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(ReplyCommand request, CancellationToken ct)
    {
        var thread = await db.MessageThreads.FirstOrDefaultAsync(t => t.Id == request.ThreadId, ct);
        var visible = thread is not null && await Inbox.Visible(db, current).AnyAsync(t => t.Id == request.ThreadId, ct);
        if (thread is null || !visible)
        {
            return Error.NotFound("Conversation not found.");
        }

        if (thread.IsBroadcast && !Inbox.IsInboxStaff(current))
        {
            return Error.Forbidden("This is an announcement; replies are switched off.");
        }

        var now = clock.UtcNow;
        var mine = await db.ThreadParticipants.FirstOrDefaultAsync(p => p.ThreadId == thread.Id && p.UserId == current.UserId, ct);
        if (mine is null)
        {
            db.ThreadParticipants.Add(new ThreadParticipant { ThreadId = thread.Id, UserId = current.UserId, LastReadAt = now });
        }
        else
        {
            mine.LastReadAt = now;
        }

        var message = new Message { ThreadId = thread.Id, SenderUserId = current.UserId, Body = request.Body.Trim() };
        db.Messages.Add(message);
        thread.LastMessageAt = now;
        await db.SaveChangesAsync(ct);

        var others = await db.ThreadParticipants.AsNoTracking().Where(p => p.ThreadId == thread.Id && p.UserId != current.UserId).Select(p => p.UserId).ToListAsync(ct);
        await publisher.Publish(new MessageSent(tenant.TenantId, thread.Id, others, Inbox.IsFamily(current)), ct);
        var name = await db.AppUsers.AsNoTracking().Where(u => u.Id == current.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct);
        return Result<MessageDto>.Success(new MessageDto(message.Id, current.UserId, name, !Inbox.IsFamily(current), message.Body, message.CreatedAt));
    }
}
