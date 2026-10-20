using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Security;
using NMS.Domain.CentreManagement;
using NMS.Domain.Communication;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Communication;

// ── Media ───────────────────────────────────────────────────────────────────

public sealed record MediaDto(Guid Id, string FileName, string ContentType, long SizeBytes);

public sealed record UploadMediaCommand(string FileName, byte[] Content) : IRequest<Result<MediaDto>>;

public sealed record MediaContent(byte[] Content, string ContentType, string FileName);

public sealed record GetMediaContentQuery(Guid Id) : IRequest<Result<MediaContent>>;

public sealed record AttachedMedia(Guid Id, string ContentType);

internal static class FileSniffer
{
    public const long MaxImageBytes = 10L * 1024 * 1024;
    public const long MaxVideoBytes = 30L * 1024 * 1024;

    /// <summary>Decides what a file really is from its first bytes, ignoring the name and the type the sender claimed.</summary>
    public static (string? Type, bool IsVideo) Detect(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", false);
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ("image/png", false);
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return ("image/gif", false);
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return ("image/webp", false);
        if (b.Length >= 12 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p') return ("video/mp4", true);
        return (null, false);
    }
}

public sealed class UploadMediaHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<UploadMediaCommand, Result<MediaDto>>
{
    public async Task<Result<MediaDto>> Handle(UploadMediaCommand request, CancellationToken ct)
    {
        var (type, isVideo) = FileSniffer.Detect(request.Content);
        if (type is null)
        {
            return Error.Validation("Only JPEG, PNG, GIF, WebP photos and MP4 videos are accepted.", "file");
        }

        if (request.Content.LongLength > (isVideo ? FileSniffer.MaxVideoBytes : FileSniffer.MaxImageBytes))
        {
            return Error.Validation(isVideo ? "Videos can be up to 30 MB." : "Photos can be up to 10 MB.", "file");
        }

        var name = Path.GetFileName(request.FileName);
        var media = new MediaFile
        {
            UploadedBy = current.UserId,
            FileName = string.IsNullOrWhiteSpace(name) ? "upload" : name[..Math.Min(name.Length, 200)],
            ContentType = type,
            SizeBytes = request.Content.LongLength,
            ScanStatus = MediaStatuses.Approved,
            ScanNote = "Checked: real file type and size",
            Content = request.Content,
        };
        db.MediaFiles.Add(media);
        await db.SaveChangesAsync(ct);
        return Result<MediaDto>.Success(new MediaDto(media.Id, media.FileName, media.ContentType, media.SizeBytes));
    }
}

public sealed class GetMediaContentHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current)
    : IRequestHandler<GetMediaContentQuery, Result<MediaContent>>
{
    public async Task<Result<MediaContent>> Handle(GetMediaContentQuery request, CancellationToken ct)
    {
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.Id && m.ScanStatus == MediaStatuses.Approved, ct);
        if (media is null)
        {
            return Error.NotFound("File not found.");
        }

        // Staff may open any file in their centre; a family only files that are in a published post about their own child.
        var restricted = await scope.GetOwnStudentIdsAsync(ct) is not null;
        if (restricted && media.UploadedBy != current.UserId)
        {
            var visible = await ActivityVisibility.PostsAsync(db, scope, current, ct);
            var allowed = await visible.Where(p => p.ApprovalStatus == ApprovalStatuses.Approved)
                .AnyAsync(p => db.ActivityPostMedia.Any(m => m.PostId == p.Id && m.MediaId == media.Id), ct);
            if (!allowed)
            {
                return Error.NotFound("File not found.");
            }
        }

        return Result<MediaContent>.Success(new MediaContent(media.Content, media.ContentType, media.FileName));
    }
}

// ── Activity posts ──────────────────────────────────────────────────────────

public sealed record ActivityDto(
    Guid Id,
    string Type,
    Dictionary<string, string>? Caption,
    Guid PostedByUserId,
    string? PostedByName,
    Guid? RoomId,
    IReadOnlyList<Guid> StudentIds,
    IReadOnlyList<AttachedMedia> Media,
    string Status,
    string? RejectionReason,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListActivitiesQuery(string? Status, Guid? StudentId, string? Cursor, int? Limit)
    : IRequest<Result<CursorPage<ActivityDto>>>;

public sealed record CreateActivityCommand(
    string Type, Dictionary<string, string>? Caption, IReadOnlyList<Guid>? StudentIds, Guid? RoomId, IReadOnlyList<Guid>? MediaIds)
    : IRequest<Result<ActivityDto>>;

public sealed record DecideActivityCommand(Guid Id, bool Approve, string? Reason) : IRequest<Result<ActivityDto>>;

/// <summary>Raised when a post becomes visible to families.</summary>
public sealed record ActivityPublished(Guid TenantId, Guid PostId, IReadOnlyList<Guid> StudentIds, Guid? RoomId) : MediatR.INotification;

public sealed class CreateActivityValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityValidator()
    {
        RuleFor(c => c.Type).Must(t => ActivityTypes.All.Contains(t)).WithMessage("Unknown activity type.");
        RuleFor(c => c.Caption!).Must(d => d.Values.All(v => v.Length <= 2000)).When(c => c.Caption is not null)
            .WithMessage("The text is too long.");
        RuleFor(c => c).Must(c => (c.Caption is { Count: > 0 } && c.Caption.Values.Any(v => !string.IsNullOrWhiteSpace(v))) || c.MediaIds is { Count: > 0 })
            .WithMessage("Add some text or a photo.");
        RuleFor(c => c).Must(c => c.Type is not (ActivityTypes.Photo or ActivityTypes.Video) || c.MediaIds is { Count: > 0 })
            .WithMessage("Photo and video posts need a file.");
        RuleFor(c => c.MediaIds!).Must(m => m.Count <= 10).When(c => c.MediaIds is not null).WithMessage("At most 10 files per post.");
    }
}

internal static class ActivityVisibility
{
    /// <summary>Posts the current person may see (before the approval filter): staff by room, families by their own children.</summary>
    public static async Task<IQueryable<ActivityPost>> PostsAsync(IAppDbContext db, IAccessScope scope, ICurrentUser current, CancellationToken ct)
    {
        var posts = db.ActivityPosts.AsNoTracking();
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            var childRooms = await db.Students.AsNoTracking().Where(s => own.Contains(s.Id) && s.RoomId != null).Select(s => s.RoomId!.Value).ToListAsync(ct);
            return posts.Where(p => p.ApprovalStatus == ApprovalStatuses.Approved
                && (db.ActivityPostStudents.Any(x => x.PostId == p.Id && own.Contains(x.StudentId))
                    || (!db.ActivityPostStudents.Any(x => x.PostId == p.Id) && (p.RoomId == null || childRooms.Contains(p.RoomId.Value)))));
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            var me = current.UserId;
            return posts.Where(p => p.PostedByUserId == me
                || (p.RoomId != null && rooms.Contains(p.RoomId.Value))
                || db.ActivityPostStudents.Any(x => x.PostId == p.Id && db.Students.Any(s => s.Id == x.StudentId && s.RoomId != null && rooms.Contains(s.RoomId.Value)))
                || (p.ApprovalStatus == ApprovalStatuses.Approved && p.RoomId == null && !db.ActivityPostStudents.Any(x => x.PostId == p.Id)));
        }

        return posts;
    }

    public static async Task<ActivityDto> ToDtoAsync(IAppDbContext db, ActivityPost post, IReadOnlyList<Guid>? ownOnly, CancellationToken ct)
    {
        var students = await db.ActivityPostStudents.AsNoTracking().Where(x => x.PostId == post.Id).Select(x => x.StudentId).ToListAsync(ct);
        if (ownOnly is not null)
        {
            students = students.Where(ownOnly.Contains).ToList();
        }

        var media = await db.ActivityPostMedia.AsNoTracking().Where(x => x.PostId == post.Id).OrderBy(x => x.SortOrder)
            .Select(x => new AttachedMedia(x.MediaId, db.MediaFiles.Where(m => m.Id == x.MediaId).Select(m => m.ContentType).First())).ToListAsync(ct);
        var name = await db.AppUsers.AsNoTracking().Where(u => u.Id == post.PostedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct);
        return new ActivityDto(post.Id, post.Type, post.Caption, post.PostedByUserId, name, post.RoomId, students, media, post.ApprovalStatus, post.RejectionReason, post.CreatedAt);
    }
}

public sealed class ListActivitiesHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current)
    : IRequestHandler<ListActivitiesQuery, Result<CursorPage<ActivityDto>>>
{
    public async Task<Result<CursorPage<ActivityDto>>> Handle(ListActivitiesQuery request, CancellationToken ct)
    {
        var posts = await ActivityVisibility.PostsAsync(db, scope, current, ct);
        if (request.Status is { } status)
        {
            posts = posts.Where(p => p.ApprovalStatus == status);
        }

        if (request.StudentId is { } studentId)
        {
            posts = posts.Where(p => db.ActivityPostStudents.Any(x => x.PostId == p.Id && x.StudentId == studentId));
        }

        var page = await posts.ToCursorPageAsync(request.Cursor, request.Limit,
            p => new ActivityHeader(p.Id, p.CreatedAt), ct);
        var own = await scope.GetOwnStudentIdsAsync(ct);
        var items = new List<ActivityDto>();
        foreach (var header in page.Items)
        {
            var post = await db.ActivityPosts.AsNoTracking().FirstAsync(p => p.Id == header.Id, ct);
            items.Add(await ActivityVisibility.ToDtoAsync(db, post, own, ct));
        }

        return Result<CursorPage<ActivityDto>>.Success(new CursorPage<ActivityDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }

    private sealed record ActivityHeader(Guid Id, DateTimeOffset CreatedAt) : ICursorItem;
}

public sealed class CreateActivityHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, ITenantContext tenant, IClock clock, IPublisher publisher)
    : IRequestHandler<CreateActivityCommand, Result<ActivityDto>>
{
    public async Task<Result<ActivityDto>> Handle(CreateActivityCommand request, CancellationToken ct)
    {
        var studentIds = (request.StudentIds ?? []).Distinct().ToList();
        if (studentIds.Count > 0)
        {
            var rooms = await scope.GetVisibleRoomIdsAsync(ct);
            var found = db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id));
            if (rooms is not null)
            {
                found = found.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
            }

            if (await found.CountAsync(ct) != studentIds.Count)
            {
                return Error.Validation("One or more children were not found.", "student_ids");
            }
        }

        if (request.RoomId is { } roomId)
        {
            var visible = await scope.GetVisibleRoomIdsAsync(ct);
            if ((visible is not null && !visible.Contains(roomId)) || !await db.Rooms.AnyAsync(r => r.Id == roomId, ct))
            {
                return Error.Validation("The room was not found.", "room_id");
            }
        }

        var mediaIds = (request.MediaIds ?? []).Distinct().ToList();
        if (mediaIds.Count > 0
            && await db.MediaFiles.CountAsync(m => mediaIds.Contains(m.Id) && m.ScanStatus == MediaStatuses.Approved && m.UploadedBy == current.UserId, ct) != mediaIds.Count)
        {
            return Error.Validation("One or more files were not found.", "media_ids");
        }

        var approved = current.HasPermission(Permissions.ActivitiesApprove) || !ActivityTypes.NeedsApproval(request.Type);
        var post = new ActivityPost
        {
            Type = request.Type,
            Caption = request.Caption?.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim()),
            PostedByUserId = current.UserId,
            RoomId = request.RoomId,
            ApprovalStatus = approved ? ApprovalStatuses.Approved : ApprovalStatuses.Pending,
            ApprovedBy = approved && current.HasPermission(Permissions.ActivitiesApprove) ? current.UserId : null,
            ApprovedAt = approved && current.HasPermission(Permissions.ActivitiesApprove) ? clock.UtcNow : null,
        };
        db.ActivityPosts.Add(post);
        foreach (var id in studentIds)
        {
            db.ActivityPostStudents.Add(new ActivityPostStudent { PostId = post.Id, StudentId = id });
        }

        var order = 0;
        foreach (var id in mediaIds)
        {
            db.ActivityPostMedia.Add(new ActivityPostMedia { PostId = post.Id, MediaId = id, SortOrder = order++ });
        }

        await db.SaveChangesAsync(ct);
        if (approved)
        {
            await publisher.Publish(new ActivityPublished(tenant.TenantId, post.Id, studentIds, post.RoomId), ct);
        }

        return Result<ActivityDto>.Success(await ActivityVisibility.ToDtoAsync(db, post, null, ct));
    }
}

public sealed class DecideActivityHandler(IAppDbContext db, ICurrentUser current, ITenantContext tenant, IClock clock, IPublisher publisher)
    : IRequestHandler<DecideActivityCommand, Result<ActivityDto>>
{
    public async Task<Result<ActivityDto>> Handle(DecideActivityCommand request, CancellationToken ct)
    {
        var post = await db.ActivityPosts.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (post is null)
        {
            return Error.NotFound("Post not found.");
        }

        if (post.ApprovalStatus != ApprovalStatuses.Pending)
        {
            return Error.InvalidState("Only a waiting post can be approved or rejected.");
        }

        post.ApprovalStatus = request.Approve ? ApprovalStatuses.Approved : ApprovalStatuses.Rejected;
        post.ApprovedBy = current.UserId;
        post.ApprovedAt = clock.UtcNow;
        post.RejectionReason = request.Approve ? null : request.Reason?.Trim();
        await db.SaveChangesAsync(ct);
        if (request.Approve)
        {
            var students = await db.ActivityPostStudents.AsNoTracking().Where(x => x.PostId == post.Id).Select(x => x.StudentId).ToListAsync(ct);
            await publisher.Publish(new ActivityPublished(tenant.TenantId, post.Id, students, post.RoomId), ct);
        }

        return Result<ActivityDto>.Success(await ActivityVisibility.ToDtoAsync(db, post, null, ct));
    }
}
