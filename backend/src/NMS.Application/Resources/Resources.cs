using System.IO.Compression;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Security;
using NMS.Domain.Communication;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Resources;

public sealed record FolderDto(Guid Id, Dictionary<string, string> Name, string Audience, Guid? ParentFolderId, int Documents);

public sealed record DocumentDto(
    Guid Id, Guid FolderId, string Title, string? Description, string? Tags, int CurrentVersion, string FileName, string ContentType, long SizeBytes, DateTimeOffset UpdatedAt, int Downloads, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record VersionDto(Guid Id, int Version, string FileName, string ContentType, long SizeBytes, string? UploadedBy, DateTimeOffset CreatedAt);

public sealed record DownloadDto(string? User, int Version, DateTimeOffset At);

public sealed record FileDownload(byte[] Content, string ContentType, string FileName);

public sealed record ListFoldersQuery : IRequest<Result<IReadOnlyList<FolderDto>>>;

public sealed record SaveFolderCommand(Guid? Id, Dictionary<string, string> Name, string Audience, Guid? ParentFolderId) : IRequest<Result<IReadOnlyList<FolderDto>>>;

public sealed record DeleteFolderCommand(Guid Id) : IRequest<Result<IReadOnlyList<FolderDto>>>;

public sealed record ListDocumentsQuery(Guid? FolderId, string? Search, string? Cursor, int? Limit) : IRequest<Result<CursorPage<DocumentDto>>>;

public sealed record UploadDocumentCommand(Guid FolderId, string Title, string? Description, string? Tags, string FileName, byte[] Content) : IRequest<Result<DocumentDto>>;

public sealed record UploadVersionCommand(Guid DocumentId, string FileName, byte[] Content) : IRequest<Result<DocumentDto>>;

public sealed record UpdateDocumentCommand(Guid Id, string Title, string? Description, string? Tags, Guid? FolderId) : IRequest<Result<DocumentDto>>;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest<Result>;

public sealed record ListVersionsQuery(Guid DocumentId) : IRequest<Result<IReadOnlyList<VersionDto>>>;

public sealed record DownloadDocumentQuery(Guid DocumentId, int? Version) : IRequest<Result<FileDownload>>;

public sealed record ListDownloadsQuery(Guid DocumentId) : IRequest<Result<IReadOnlyList<DownloadDto>>>;

/// <summary>Decides what an uploaded file really is from its first bytes, ignoring the name and the type the sender claimed.</summary>
public static class DocumentSniffer
{
    public const long MaxBytes = 30L * 1024 * 1024;

    public static string? Detect(byte[] b)
    {
        if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-') return "application/pdf";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return "image/gif";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        if (b.Length >= 12 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p') return "video/mp4";
        if (b.Length >= 4 && b[0] == 'P' && b[1] == 'K' && b[2] == 3 && b[3] == 4)
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(b), ZipArchiveMode.Read);
                if (zip.GetEntry("word/document.xml") is not null) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                if (zip.GetEntry("xl/workbook.xml") is not null) return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        return null;
    }
}

public sealed class SaveFolderValidator : AbstractValidator<SaveFolderCommand>
{
    public SaveFolderValidator()
    {
        RuleFor(x => x.Name).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 100).WithMessage("Give the folder an English name (up to 100 characters).");
        RuleFor(x => x.Audience).Must(a => ResourceAudiences.All.Contains(a)).WithMessage("Audience must be parents or staff.");
    }
}

public sealed class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Tags).MaximumLength(300);
    }
}

public sealed class UpdateDocumentValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Tags).MaximumLength(300);
    }
}

internal static class ResourceScope
{
    /// <summary>A family (a parent account) sees only the folders made for parents; staff see every folder.</summary>
    public static async Task<bool> IsFamilyAsync(IAccessScope scope, CancellationToken ct) => await scope.GetOwnStudentIdsAsync(ct) is not null;

    public static async Task<List<FolderDto>> FoldersAsync(IAppDbContext db, bool family, CancellationToken ct)
    {
        var q = db.ResourceFolders.AsNoTracking().AsQueryable();
        if (family) q = q.Where(f => f.Audience == ResourceAudiences.Parents);
        var counts = await db.ResourceDocuments.AsNoTracking().GroupBy(d => d.FolderId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        return (await q.ToListAsync(ct)).OrderBy(f => f.Name.GetValueOrDefault("en")).Select(f => new FolderDto(f.Id, f.Name, f.Audience, f.ParentFolderId, counts.GetValueOrDefault(f.Id))).ToList();
    }

    public static async Task<DocumentDto> DocumentAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var d = await db.ResourceDocuments.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var v = await db.ResourceVersions.AsNoTracking().Where(x => x.DocumentId == id && x.Version == d.CurrentVersion).Select(x => new { x.FileName, x.ContentType, x.SizeBytes }).FirstAsync(ct);
        var downloads = await db.ResourceDownloads.CountAsync(x => x.DocumentId == id, ct);
        return new DocumentDto(d.Id, d.FolderId, d.Title, d.Description, d.Tags, d.CurrentVersion, v.FileName, v.ContentType, v.SizeBytes, d.UpdatedAt, downloads, d.CreatedAt);
    }
}

public sealed class ListFoldersHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListFoldersQuery, Result<IReadOnlyList<FolderDto>>>
{
    public async Task<Result<IReadOnlyList<FolderDto>>> Handle(ListFoldersQuery request, CancellationToken ct) =>
        Result<IReadOnlyList<FolderDto>>.Success(await ResourceScope.FoldersAsync(db, await ResourceScope.IsFamilyAsync(scope, ct), ct));
}

public sealed class SaveFolderHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<SaveFolderCommand, Result<IReadOnlyList<FolderDto>>>
{
    public async Task<Result<IReadOnlyList<FolderDto>>> Handle(SaveFolderCommand request, CancellationToken ct)
    {
        if (request.ParentFolderId is { } parent && !await db.ResourceFolders.AnyAsync(f => f.Id == parent, ct))
        {
            return Error.Validation("The parent folder does not exist.", "parent_folder_id");
        }

        ResourceFolder folder;
        if (request.Id is { } id)
        {
            var found = await db.ResourceFolders.FirstOrDefaultAsync(f => f.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Folder not found.");
            }

            if (request.ParentFolderId == id)
            {
                return Error.Validation("A folder cannot be inside itself.", "parent_folder_id");
            }

            folder = found;
        }
        else
        {
            folder = new ResourceFolder();
            db.ResourceFolders.Add(folder);
        }

        folder.Name = request.Name.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary(n => n.Key, n => n.Value.Trim());
        folder.Audience = request.Audience;
        folder.ParentFolderId = request.ParentFolderId;
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<FolderDto>>.Success(await ResourceScope.FoldersAsync(db, await ResourceScope.IsFamilyAsync(scope, ct), ct));
    }
}

public sealed class DeleteFolderHandler(IAppDbContext db, IAccessScope scope, IClock clock) : IRequestHandler<DeleteFolderCommand, Result<IReadOnlyList<FolderDto>>>
{
    public async Task<Result<IReadOnlyList<FolderDto>>> Handle(DeleteFolderCommand request, CancellationToken ct)
    {
        var folder = await db.ResourceFolders.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (folder is null)
        {
            return Error.NotFound("Folder not found.");
        }

        if (await db.ResourceDocuments.AnyAsync(d => d.FolderId == folder.Id, ct) || await db.ResourceFolders.AnyAsync(f => f.ParentFolderId == folder.Id, ct))
        {
            return Error.Conflict("Move or remove what is inside this folder first.");
        }

        folder.IsDeleted = true;
        folder.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<FolderDto>>.Success(await ResourceScope.FoldersAsync(db, await ResourceScope.IsFamilyAsync(scope, ct), ct));
    }
}

public sealed class ListDocumentsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListDocumentsQuery, Result<CursorPage<DocumentDto>>>
{
    public async Task<Result<CursorPage<DocumentDto>>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var q = db.ResourceDocuments.AsNoTracking().AsQueryable();
        if (await ResourceScope.IsFamilyAsync(scope, ct))
        {
            q = q.Where(d => db.ResourceFolders.Any(f => f.Id == d.FolderId && f.Audience == ResourceAudiences.Parents));
        }

        if (request.FolderId is { } folder) q = q.Where(d => d.FolderId == folder);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(d => d.Title.Contains(term) || (d.Tags != null && d.Tags.Contains(term)) || (d.Description != null && d.Description.Contains(term)));
        }

        var page = await q.ToCursorPageAsync(request.Cursor, request.Limit,
            d => new DocumentDto(
                d.Id, d.FolderId, d.Title, d.Description, d.Tags, d.CurrentVersion,
                db.ResourceVersions.Where(v => v.DocumentId == d.Id && v.Version == d.CurrentVersion).Select(v => v.FileName).FirstOrDefault() ?? string.Empty,
                db.ResourceVersions.Where(v => v.DocumentId == d.Id && v.Version == d.CurrentVersion).Select(v => v.ContentType).FirstOrDefault() ?? string.Empty,
                db.ResourceVersions.Where(v => v.DocumentId == d.Id && v.Version == d.CurrentVersion).Select(v => v.SizeBytes).FirstOrDefault(),
                d.UpdatedAt, db.ResourceDownloads.Count(x => x.DocumentId == d.Id), d.CreatedAt), ct);
        return Result<CursorPage<DocumentDto>>.Success(page);
    }
}

public sealed class UploadDocumentHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<UploadDocumentCommand, Result<DocumentDto>>
{
    public async Task<Result<DocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (!await db.ResourceFolders.AnyAsync(f => f.Id == request.FolderId, ct))
        {
            return Error.Validation("Choose an existing folder.", "folder_id");
        }

        var type = DocumentSniffer.Detect(request.Content);
        if (type is null)
        {
            return Error.Validation("Only PDF, Word, Excel, photo (JPEG, PNG, GIF, WebP) and MP4 files are accepted.", "file");
        }

        if (request.Content.LongLength > DocumentSniffer.MaxBytes)
        {
            return Error.Validation("Files can be up to 30 MB.", "file");
        }

        var doc = new ResourceDocument { FolderId = request.FolderId, Title = request.Title.Trim(), Description = request.Description?.Trim(), Tags = request.Tags?.Trim(), CurrentVersion = 1 };
        db.ResourceDocuments.Add(doc);
        db.ResourceVersions.Add(new ResourceVersion
        {
            DocumentId = doc.Id, Version = 1, FileName = Path.GetFileName(request.FileName) is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : "file",
            ContentType = type, SizeBytes = request.Content.LongLength, Content = request.Content, UploadedBy = current.UserId,
        });
        await db.SaveChangesAsync(ct);
        return Result<DocumentDto>.Success(await ResourceScope.DocumentAsync(db, doc.Id, ct));
    }
}

public sealed class UploadVersionHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<UploadVersionCommand, Result<DocumentDto>>
{
    public async Task<Result<DocumentDto>> Handle(UploadVersionCommand request, CancellationToken ct)
    {
        var doc = await db.ResourceDocuments.FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);
        if (doc is null)
        {
            return Error.NotFound("Document not found.");
        }

        var type = DocumentSniffer.Detect(request.Content);
        if (type is null)
        {
            return Error.Validation("Only PDF, Word, Excel, photo (JPEG, PNG, GIF, WebP) and MP4 files are accepted.", "file");
        }

        if (request.Content.LongLength > DocumentSniffer.MaxBytes)
        {
            return Error.Validation("Files can be up to 30 MB.", "file");
        }

        doc.CurrentVersion++;
        db.ResourceVersions.Add(new ResourceVersion
        {
            DocumentId = doc.Id, Version = doc.CurrentVersion, FileName = Path.GetFileName(request.FileName) is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : "file",
            ContentType = type, SizeBytes = request.Content.LongLength, Content = request.Content, UploadedBy = current.UserId,
        });
        await db.SaveChangesAsync(ct);
        return Result<DocumentDto>.Success(await ResourceScope.DocumentAsync(db, doc.Id, ct));
    }
}

public sealed class UpdateDocumentHandler(IAppDbContext db) : IRequestHandler<UpdateDocumentCommand, Result<DocumentDto>>
{
    public async Task<Result<DocumentDto>> Handle(UpdateDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.ResourceDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null)
        {
            return Error.NotFound("Document not found.");
        }

        if (request.FolderId is { } folder)
        {
            if (!await db.ResourceFolders.AnyAsync(f => f.Id == folder, ct))
            {
                return Error.Validation("Choose an existing folder.", "folder_id");
            }

            doc.FolderId = folder;
        }

        doc.Title = request.Title.Trim();
        doc.Description = request.Description?.Trim();
        doc.Tags = request.Tags?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<DocumentDto>.Success(await ResourceScope.DocumentAsync(db, doc.Id, ct));
    }
}

public sealed class DeleteDocumentHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.ResourceDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null)
        {
            return Result.Failure(Error.NotFound("Document not found."));
        }

        doc.IsDeleted = true;
        doc.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ListVersionsHandler(IAppDbContext db) : IRequestHandler<ListVersionsQuery, Result<IReadOnlyList<VersionDto>>>
{
    public async Task<Result<IReadOnlyList<VersionDto>>> Handle(ListVersionsQuery request, CancellationToken ct)
    {
        if (!await db.ResourceDocuments.AnyAsync(d => d.Id == request.DocumentId, ct))
        {
            return Error.NotFound("Document not found.");
        }

        var rows = await db.ResourceVersions.AsNoTracking().Where(v => v.DocumentId == request.DocumentId).OrderByDescending(v => v.Version)
            .Select(v => new { v.Id, v.Version, v.FileName, v.ContentType, v.SizeBytes, v.UploadedBy, v.CreatedAt }).ToListAsync(ct);
        var users = rows.Select(r => r.UploadedBy).Distinct().ToList();
        var names = await db.AppUsers.AsNoTracking().Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return Result<IReadOnlyList<VersionDto>>.Success(rows.Select(r => new VersionDto(r.Id, r.Version, r.FileName, r.ContentType, r.SizeBytes, names.GetValueOrDefault(r.UploadedBy), r.CreatedAt)).ToList());
    }
}

/// <summary>Gives a file to the person who asked, after checking they may see its folder, and records the download. Staff can fetch older versions; families get the current one.</summary>
public sealed class DownloadDocumentHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current) : IRequestHandler<DownloadDocumentQuery, Result<FileDownload>>
{
    public async Task<Result<FileDownload>> Handle(DownloadDocumentQuery request, CancellationToken ct)
    {
        var doc = await db.ResourceDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);
        var family = await ResourceScope.IsFamilyAsync(scope, ct);
        if (doc is null || (family && !await db.ResourceFolders.AnyAsync(f => f.Id == doc.FolderId && f.Audience == ResourceAudiences.Parents, ct)))
        {
            return Error.NotFound("Document not found.");
        }

        var number = family || request.Version is null ? doc.CurrentVersion : request.Version.Value;
        var version = await db.ResourceVersions.AsNoTracking().FirstOrDefaultAsync(v => v.DocumentId == doc.Id && v.Version == number, ct);
        if (version is null)
        {
            return Error.NotFound("That version does not exist.");
        }

        db.ResourceDownloads.Add(new ResourceDownload { DocumentId = doc.Id, VersionId = version.Id, UserId = current.UserId });
        await db.SaveChangesAsync(ct);
        return Result<FileDownload>.Success(new FileDownload(version.Content, version.ContentType, version.FileName));
    }
}

public sealed class ListDownloadsHandler(IAppDbContext db) : IRequestHandler<ListDownloadsQuery, Result<IReadOnlyList<DownloadDto>>>
{
    public async Task<Result<IReadOnlyList<DownloadDto>>> Handle(ListDownloadsQuery request, CancellationToken ct)
    {
        if (!await db.ResourceDocuments.AnyAsync(d => d.Id == request.DocumentId, ct))
        {
            return Error.NotFound("Document not found.");
        }

        var rows = await db.ResourceDownloads.AsNoTracking().Where(d => d.DocumentId == request.DocumentId).OrderByDescending(d => d.CreatedAt).Take(500)
            .Select(d => new { d.UserId, d.VersionId, d.CreatedAt }).ToListAsync(ct);
        var users = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await db.AppUsers.AsNoTracking().Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        var versions = await db.ResourceVersions.AsNoTracking().Where(v => v.DocumentId == request.DocumentId).ToDictionaryAsync(v => v.Id, v => v.Version, ct);
        return Result<IReadOnlyList<DownloadDto>>.Success(rows.Select(r => new DownloadDto(names.GetValueOrDefault(r.UserId), versions.GetValueOrDefault(r.VersionId), r.CreatedAt)).ToList());
    }
}
