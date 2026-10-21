using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Learning;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Learning;

using Text = Dictionary<string, string>;

public sealed record FrameworkSummaryDto(Guid Id, string Code, Text Name, Text? Description, int Areas, int AgeBands, int Milestones, int Lessons);

public sealed record AreaDto(Guid Id, string Code, Text Name, int SortOrder);

public sealed record AgeBandDto(Guid Id, string Code, Text Name, int MinMonths, int MaxMonths);

public sealed record MilestoneDto(Guid Id, Guid AreaId, Guid AgeBandId, string Code, Text Statement, int SortOrder);

public sealed record FrameworkDetailDto(
    Guid Id, string Code, Text Name, Text? Description, IReadOnlyList<AreaDto> Areas, IReadOnlyList<AgeBandDto> AgeBands, IReadOnlyList<MilestoneDto> Milestones);

public sealed record LessonDto(
    Guid Id, Text Title, Text? Description, Guid FrameworkId, Guid? AreaId, Guid? AgeBandId, Guid? RoomId, int DurationMinutes,
    IReadOnlyList<string> Materials, IReadOnlyList<string> Objectives, Dictionary<string, string> Links, IReadOnlyList<Guid> MilestoneIds, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListFrameworksQuery : IRequest<Result<IReadOnlyList<FrameworkSummaryDto>>>;

public sealed record GetFrameworkQuery(Guid Id) : IRequest<Result<FrameworkDetailDto>>;

public sealed record SaveFrameworkCommand(Guid? Id, string Code, Text Name, Text? Description) : IRequest<Result<FrameworkSummaryDto>>;

public sealed record DeleteFrameworkCommand(Guid Id) : IRequest<Result>;

public sealed record ImportArea(string Code, Text Name, int? SortOrder);

public sealed record ImportBand(string Code, Text Name, int MinMonths, int MaxMonths);

public sealed record ImportMilestone(string Area, string AgeBand, string Code, Text Statement);

/// <summary>Adds or updates areas, age bands and milestones of a framework by their codes in one go. Used for the first load and for later changes.</summary>
public sealed record ImportFrameworkCommand(Guid Id, IReadOnlyList<ImportArea>? Areas, IReadOnlyList<ImportBand>? AgeBands, IReadOnlyList<ImportMilestone>? Milestones)
    : IRequest<Result<FrameworkDetailDto>>;

public sealed record SaveAreaCommand(Guid FrameworkId, Guid? Id, string Code, Text Name, int? SortOrder) : IRequest<Result<FrameworkDetailDto>>;

public sealed record SaveBandCommand(Guid FrameworkId, Guid? Id, string Code, Text Name, int MinMonths, int MaxMonths) : IRequest<Result<FrameworkDetailDto>>;

public sealed record SaveMilestoneCommand(Guid FrameworkId, Guid? Id, Guid AreaId, Guid AgeBandId, string Code, Text Statement) : IRequest<Result<FrameworkDetailDto>>;

public sealed record DeletePartCommand(Guid FrameworkId, string Kind, Guid Id) : IRequest<Result<FrameworkDetailDto>>;

public sealed record ListLessonsQuery(Guid? FrameworkId, Guid? AreaId, Guid? AgeBandId, Guid? RoomId, string? Search, string? Cursor, int? Limit) : IRequest<Result<CursorPage<LessonDto>>>;

public sealed record GetLessonQuery(Guid Id) : IRequest<Result<LessonDto>>;

public sealed record SaveLessonCommand(
    Guid? Id, Text Title, Text? Description, Guid FrameworkId, Guid? AreaId, Guid? AgeBandId, Guid? RoomId, int DurationMinutes,
    IReadOnlyList<string>? Materials, IReadOnlyList<string>? Objectives, Dictionary<string, string>? Links, IReadOnlyList<Guid>? MilestoneIds) : IRequest<Result<LessonDto>>;

public sealed record DeleteLessonCommand(Guid Id) : IRequest<Result>;

internal static partial class LearningRules
{
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,38}$")]
    public static partial Regex CodePattern();

    public static bool HasText(Text? text) => text is { Count: > 0 } && text.Values.Any(v => !string.IsNullOrWhiteSpace(v)) && text.Values.All(v => v is null || v.Length <= 2000);
}

public sealed class SaveFrameworkValidator : AbstractValidator<SaveFrameworkCommand>
{
    public SaveFrameworkValidator()
    {
        RuleFor(c => c.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("The code uses lower-case letters, digits, - or _ (up to 39).");
        RuleFor(c => c.Name).Must(LearningRules.HasText).WithMessage("A name is required.");
    }
}

public sealed class ImportFrameworkValidator : AbstractValidator<ImportFrameworkCommand>
{
    public ImportFrameworkValidator()
    {
        RuleFor(c => c.Areas).Must(a => a is null || a.Count <= 50).WithMessage("At most 50 areas at a time.");
        RuleFor(c => c.AgeBands).Must(a => a is null || a.Count <= 50).WithMessage("At most 50 age bands at a time.");
        RuleFor(c => c.Milestones).Must(a => a is null || a.Count <= 2000).WithMessage("At most 2000 milestones at a time.");
        RuleForEach(c => c.Areas!).ChildRules(a =>
        {
            a.RuleFor(x => x.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("Area codes use lower-case letters, digits, - or _.");
            a.RuleFor(x => x.Name).Must(LearningRules.HasText).WithMessage("Every area needs a name.");
        }).When(c => c.Areas is not null);
        RuleForEach(c => c.AgeBands!).ChildRules(b =>
        {
            b.RuleFor(x => x.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("Age band codes use lower-case letters, digits, - or _.");
            b.RuleFor(x => x.Name).Must(LearningRules.HasText).WithMessage("Every age band needs a name.");
            b.RuleFor(x => x.MinMonths).InclusiveBetween(0, 120);
            b.RuleFor(x => x.MaxMonths).InclusiveBetween(1, 120);
            b.RuleFor(x => x).Must(x => x.MaxMonths > x.MinMonths).WithMessage("The band must end after it starts.");
        }).When(c => c.AgeBands is not null);
        RuleForEach(c => c.Milestones!).ChildRules(m =>
        {
            m.RuleFor(x => x.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("Milestone codes use lower-case letters, digits, - or _.");
            m.RuleFor(x => x.Statement).Must(LearningRules.HasText).WithMessage("Every milestone needs a statement.");
        }).When(c => c.Milestones is not null);
    }
}

public sealed class SaveAreaValidator : AbstractValidator<SaveAreaCommand>
{
    public SaveAreaValidator()
    {
        RuleFor(c => c.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("The code uses lower-case letters, digits, - or _.");
        RuleFor(c => c.Name).Must(LearningRules.HasText).WithMessage("A name is required.");
    }
}

public sealed class SaveBandValidator : AbstractValidator<SaveBandCommand>
{
    public SaveBandValidator()
    {
        RuleFor(c => c.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("The code uses lower-case letters, digits, - or _.");
        RuleFor(c => c.Name).Must(LearningRules.HasText).WithMessage("A name is required.");
        RuleFor(c => c.MinMonths).InclusiveBetween(0, 120);
        RuleFor(c => c.MaxMonths).InclusiveBetween(1, 120);
        RuleFor(c => c).Must(c => c.MaxMonths > c.MinMonths).WithMessage("The band must end after it starts.");
    }
}

public sealed class SaveMilestoneValidator : AbstractValidator<SaveMilestoneCommand>
{
    public SaveMilestoneValidator()
    {
        RuleFor(c => c.Code).Must(c => LearningRules.CodePattern().IsMatch(c ?? string.Empty)).WithMessage("The code uses lower-case letters, digits, - or _.");
        RuleFor(c => c.Statement).Must(LearningRules.HasText).WithMessage("A statement is required.");
    }
}

public sealed class DeletePartValidator : AbstractValidator<DeletePartCommand>
{
    public DeletePartValidator() => RuleFor(c => c.Kind).Must(k => k is "area" or "age_band" or "milestone").WithMessage("Kind must be area, age_band or milestone.");
}

public sealed class SaveLessonValidator : AbstractValidator<SaveLessonCommand>
{
    public SaveLessonValidator()
    {
        RuleFor(c => c.Title).Must(LearningRules.HasText).WithMessage("A title is required.");
        RuleFor(c => c.DurationMinutes).InclusiveBetween(5, 480);
        RuleFor(c => c.Materials).Must(m => m is null || (m.Count <= 40 && m.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 200))).WithMessage("Up to 40 materials, each up to 200 characters.");
        RuleFor(c => c.Objectives).Must(m => m is null || (m.Count <= 40 && m.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 400))).WithMessage("Up to 40 objectives, each up to 400 characters.");
        RuleFor(c => c.Links).Must(l => l is null || (l.Count <= 20 && l.Keys.All(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))).WithMessage("Up to 20 links, each a web address (http or https).");
    }
}

internal static class LearningMap
{
    public static async Task<FrameworkDetailDto?> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var f = await db.CurriculumFrameworks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null)
        {
            return null;
        }

        var areas = await db.DevelopmentAreas.AsNoTracking().Where(a => a.FrameworkId == id).OrderBy(a => a.SortOrder).ThenBy(a => a.Code).Select(a => new AreaDto(a.Id, a.Code, a.Name, a.SortOrder)).ToListAsync(ct);
        var bands = await db.AgeBands.AsNoTracking().Where(a => a.FrameworkId == id).OrderBy(a => a.MinMonths).ThenBy(a => a.MaxMonths).Select(a => new AgeBandDto(a.Id, a.Code, a.Name, a.MinMonths, a.MaxMonths)).ToListAsync(ct);
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.FrameworkId == id).OrderBy(m => m.SortOrder).ThenBy(m => m.Code).Select(m => new MilestoneDto(m.Id, m.AreaId, m.AgeBandId, m.Code, m.Statement, m.SortOrder)).ToListAsync(ct);
        return new FrameworkDetailDto(f.Id, f.Code, f.Name, f.Description, areas, bands, milestones);
    }

    public static LessonDto ToDto(Lesson l) =>
        new(l.Id, l.Title, l.Description, l.FrameworkId, l.AreaId, l.AgeBandId, l.RoomId, l.DurationMinutes, l.Materials, l.Objectives, l.Links,
            l.MilestoneIds.Select(Guid.Parse).ToList(), l.CreatedAt);
}

public sealed class ListFrameworksHandler(IAppDbContext db) : IRequestHandler<ListFrameworksQuery, Result<IReadOnlyList<FrameworkSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<FrameworkSummaryDto>>> Handle(ListFrameworksQuery request, CancellationToken ct)
    {
        var frameworks = await db.CurriculumFrameworks.AsNoTracking().OrderBy(f => f.Code).ToListAsync(ct);
        var areas = await db.DevelopmentAreas.AsNoTracking().GroupBy(a => a.FrameworkId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var bands = await db.AgeBands.AsNoTracking().GroupBy(a => a.FrameworkId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var milestones = await db.Milestones.AsNoTracking().GroupBy(a => a.FrameworkId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var lessons = await db.Lessons.AsNoTracking().GroupBy(a => a.FrameworkId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return Result<IReadOnlyList<FrameworkSummaryDto>>.Success(frameworks.Select(f => new FrameworkSummaryDto(
            f.Id, f.Code, f.Name, f.Description, areas.GetValueOrDefault(f.Id), bands.GetValueOrDefault(f.Id), milestones.GetValueOrDefault(f.Id), lessons.GetValueOrDefault(f.Id))).ToList());
    }
}

public sealed class GetFrameworkHandler(IAppDbContext db) : IRequestHandler<GetFrameworkQuery, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(GetFrameworkQuery request, CancellationToken ct) =>
        await LearningMap.DetailAsync(db, request.Id, ct) is { } detail ? Result<FrameworkDetailDto>.Success(detail) : Error.NotFound("Framework not found.");
}

public sealed class SaveFrameworkHandler(IAppDbContext db) : IRequestHandler<SaveFrameworkCommand, Result<FrameworkSummaryDto>>
{
    public async Task<Result<FrameworkSummaryDto>> Handle(SaveFrameworkCommand request, CancellationToken ct)
    {
        var duplicate = await db.CurriculumFrameworks.AnyAsync(f => f.Code == request.Code && f.Id != (request.Id ?? Guid.Empty), ct);
        if (duplicate)
        {
            return Error.Conflict("A framework with this code already exists.");
        }

        CurriculumFramework row;
        if (request.Id is { } id)
        {
            row = await db.CurriculumFrameworks.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new KeyNotFoundException();
        }
        else
        {
            row = new CurriculumFramework();
            db.CurriculumFrameworks.Add(row);
        }

        row.Code = request.Code;
        row.Name = request.Name;
        row.Description = request.Description;
        await db.SaveChangesAsync(ct);
        return Result<FrameworkSummaryDto>.Success(new FrameworkSummaryDto(row.Id, row.Code, row.Name, row.Description,
            await db.DevelopmentAreas.CountAsync(a => a.FrameworkId == row.Id, ct), await db.AgeBands.CountAsync(a => a.FrameworkId == row.Id, ct),
            await db.Milestones.CountAsync(a => a.FrameworkId == row.Id, ct), await db.Lessons.CountAsync(a => a.FrameworkId == row.Id, ct)));
    }
}

public sealed class DeleteFrameworkHandler(IAppDbContext db) : IRequestHandler<DeleteFrameworkCommand, Result>
{
    public async Task<Result> Handle(DeleteFrameworkCommand request, CancellationToken ct)
    {
        var row = await db.CurriculumFrameworks.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (row is null)
        {
            return Result.Failure(Error.NotFound("Framework not found."));
        }

        if (await db.Lessons.AnyAsync(l => l.FrameworkId == row.Id, ct))
        {
            return Result.Failure(Error.InvalidState("Delete or move this framework's lessons first."));
        }

        db.Milestones.RemoveRange(await db.Milestones.Where(m => m.FrameworkId == row.Id).ToListAsync(ct));
        db.DevelopmentAreas.RemoveRange(await db.DevelopmentAreas.Where(m => m.FrameworkId == row.Id).ToListAsync(ct));
        db.AgeBands.RemoveRange(await db.AgeBands.Where(m => m.FrameworkId == row.Id).ToListAsync(ct));
        db.CurriculumFrameworks.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ImportFrameworkHandler(IAppDbContext db) : IRequestHandler<ImportFrameworkCommand, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(ImportFrameworkCommand request, CancellationToken ct)
    {
        if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == request.Id, ct))
        {
            return Error.NotFound("Framework not found.");
        }

        var areas = await db.DevelopmentAreas.Where(a => a.FrameworkId == request.Id).ToListAsync(ct);
        var bands = await db.AgeBands.Where(a => a.FrameworkId == request.Id).ToListAsync(ct);
        var existing = await db.Milestones.Where(a => a.FrameworkId == request.Id).ToListAsync(ct);

        var order = areas.Count;
        foreach (var a in request.Areas ?? [])
        {
            var row = areas.FirstOrDefault(x => x.Code == a.Code);
            if (row is null)
            {
                row = new DevelopmentArea { FrameworkId = request.Id, Code = a.Code, SortOrder = a.SortOrder ?? ++order };
                db.DevelopmentAreas.Add(row);
                areas.Add(row);
            }

            row.Name = a.Name;
            if (a.SortOrder is { } s)
            {
                row.SortOrder = s;
            }
        }

        foreach (var b in request.AgeBands ?? [])
        {
            var row = bands.FirstOrDefault(x => x.Code == b.Code);
            if (row is null)
            {
                row = new AgeBand { FrameworkId = request.Id, Code = b.Code };
                db.AgeBands.Add(row);
                bands.Add(row);
            }

            row.Name = b.Name;
            row.MinMonths = b.MinMonths;
            row.MaxMonths = b.MaxMonths;
        }

        var seen = new HashSet<string>();
        var sort = existing.Count;
        foreach (var m in request.Milestones ?? [])
        {
            var area = areas.FirstOrDefault(a => a.Code == m.Area);
            var band = bands.FirstOrDefault(b => b.Code == m.AgeBand);
            if (area is null || band is null)
            {
                return Error.Validation($"Milestone {m.Code}: unknown area or age band.", "milestones");
            }

            if (!seen.Add(m.Code))
            {
                return Error.Validation($"Milestone code {m.Code} appears twice.", "milestones");
            }

            var row = existing.FirstOrDefault(x => x.Code == m.Code);
            if (row is null)
            {
                row = new Milestone { FrameworkId = request.Id, Code = m.Code, SortOrder = ++sort };
                db.Milestones.Add(row);
                existing.Add(row);
            }

            row.Statement = m.Statement;
            row.AreaId = area.Id;
            row.AgeBandId = band.Id;
        }

        await db.SaveChangesAsync(ct);
        return Result<FrameworkDetailDto>.Success((await LearningMap.DetailAsync(db, request.Id, ct))!);
    }
}

public sealed class SaveAreaHandler(IAppDbContext db) : IRequestHandler<SaveAreaCommand, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(SaveAreaCommand request, CancellationToken ct)
    {
        if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == request.FrameworkId, ct))
        {
            return Error.NotFound("Framework not found.");
        }

        var all = await db.DevelopmentAreas.Where(a => a.FrameworkId == request.FrameworkId).ToListAsync(ct);
        if (all.Any(a => a.Code == request.Code && a.Id != request.Id))
        {
            return Error.Conflict("An area with this code already exists.");
        }

        var row = request.Id is { } id ? all.FirstOrDefault(a => a.Id == id) : null;
        if (request.Id is not null && row is null)
        {
            return Error.NotFound("Area not found.");
        }

        if (row is null)
        {
            row = new DevelopmentArea { FrameworkId = request.FrameworkId, SortOrder = all.Count + 1 };
            db.DevelopmentAreas.Add(row);
        }

        row.Code = request.Code;
        row.Name = request.Name;
        if (request.SortOrder is { } s)
        {
            row.SortOrder = s;
        }

        await db.SaveChangesAsync(ct);
        return Result<FrameworkDetailDto>.Success((await LearningMap.DetailAsync(db, request.FrameworkId, ct))!);
    }
}

public sealed class SaveBandHandler(IAppDbContext db) : IRequestHandler<SaveBandCommand, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(SaveBandCommand request, CancellationToken ct)
    {
        if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == request.FrameworkId, ct))
        {
            return Error.NotFound("Framework not found.");
        }

        var all = await db.AgeBands.Where(a => a.FrameworkId == request.FrameworkId).ToListAsync(ct);
        if (all.Any(a => a.Code == request.Code && a.Id != request.Id))
        {
            return Error.Conflict("An age band with this code already exists.");
        }

        var row = request.Id is { } id ? all.FirstOrDefault(a => a.Id == id) : null;
        if (request.Id is not null && row is null)
        {
            return Error.NotFound("Age band not found.");
        }

        if (row is null)
        {
            row = new AgeBand { FrameworkId = request.FrameworkId };
            db.AgeBands.Add(row);
        }

        row.Code = request.Code;
        row.Name = request.Name;
        row.MinMonths = request.MinMonths;
        row.MaxMonths = request.MaxMonths;
        await db.SaveChangesAsync(ct);
        return Result<FrameworkDetailDto>.Success((await LearningMap.DetailAsync(db, request.FrameworkId, ct))!);
    }
}

public sealed class SaveMilestoneHandler(IAppDbContext db) : IRequestHandler<SaveMilestoneCommand, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(SaveMilestoneCommand request, CancellationToken ct)
    {
        if (!await db.DevelopmentAreas.AnyAsync(a => a.Id == request.AreaId && a.FrameworkId == request.FrameworkId, ct)
            || !await db.AgeBands.AnyAsync(a => a.Id == request.AgeBandId && a.FrameworkId == request.FrameworkId, ct))
        {
            return Error.Validation("The area or age band is not part of this framework.", "area_id");
        }

        var all = await db.Milestones.Where(m => m.FrameworkId == request.FrameworkId).ToListAsync(ct);
        if (all.Any(m => m.Code == request.Code && m.Id != request.Id))
        {
            return Error.Conflict("A milestone with this code already exists.");
        }

        var row = request.Id is { } id ? all.FirstOrDefault(m => m.Id == id) : null;
        if (request.Id is not null && row is null)
        {
            return Error.NotFound("Milestone not found.");
        }

        if (row is null)
        {
            row = new Milestone { FrameworkId = request.FrameworkId, SortOrder = all.Count + 1 };
            db.Milestones.Add(row);
        }

        row.AreaId = request.AreaId;
        row.AgeBandId = request.AgeBandId;
        row.Code = request.Code;
        row.Statement = request.Statement;
        await db.SaveChangesAsync(ct);
        return Result<FrameworkDetailDto>.Success((await LearningMap.DetailAsync(db, request.FrameworkId, ct))!);
    }
}

public sealed class DeletePartHandler(IAppDbContext db) : IRequestHandler<DeletePartCommand, Result<FrameworkDetailDto>>
{
    public async Task<Result<FrameworkDetailDto>> Handle(DeletePartCommand request, CancellationToken ct)
    {
        var fw = request.FrameworkId;
        switch (request.Kind)
        {
            case "milestone":
                var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == request.Id && m.FrameworkId == fw, ct);
                if (milestone is null)
                {
                    return Error.NotFound("Milestone not found.");
                }

                var tag = milestone.Id.ToString();
                if ((await db.Lessons.Where(l => l.FrameworkId == fw).ToListAsync(ct)).Any(l => l.MilestoneIds.Contains(tag)))
                {
                    return Error.InvalidState("A lesson uses this milestone. Remove it from the lesson first.");
                }

                db.Milestones.Remove(milestone);
                break;
            case "area":
                var area = await db.DevelopmentAreas.FirstOrDefaultAsync(a => a.Id == request.Id && a.FrameworkId == fw, ct);
                if (area is null)
                {
                    return Error.NotFound("Area not found.");
                }

                if (await db.Milestones.AnyAsync(m => m.AreaId == area.Id, ct) || await db.Lessons.AnyAsync(l => l.AreaId == area.Id, ct))
                {
                    return Error.InvalidState("Remove this area's milestones and lessons first.");
                }

                db.DevelopmentAreas.Remove(area);
                break;
            default:
                var band = await db.AgeBands.FirstOrDefaultAsync(a => a.Id == request.Id && a.FrameworkId == fw, ct);
                if (band is null)
                {
                    return Error.NotFound("Age band not found.");
                }

                if (await db.Milestones.AnyAsync(m => m.AgeBandId == band.Id, ct) || await db.Lessons.AnyAsync(l => l.AgeBandId == band.Id, ct))
                {
                    return Error.InvalidState("Remove this age band's milestones and lessons first.");
                }

                db.AgeBands.Remove(band);
                break;
        }

        await db.SaveChangesAsync(ct);
        return Result<FrameworkDetailDto>.Success((await LearningMap.DetailAsync(db, fw, ct))!);
    }
}

internal static class LessonScope
{
    /// <summary>Lessons for everyone, or for a room the person can see. Managers see all.</summary>
    public static async Task<IQueryable<Lesson>> VisibleAsync(IAppDbContext db, IAccessScope scope, CancellationToken ct)
    {
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        var all = db.Lessons.AsNoTracking();
        return rooms is null ? all : all.Where(l => l.RoomId == null || rooms.Contains(l.RoomId.Value));
    }
}

public sealed class ListLessonsHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<ListLessonsQuery, Result<CursorPage<LessonDto>>>
{
    public async Task<Result<CursorPage<LessonDto>>> Handle(ListLessonsQuery request, CancellationToken ct)
    {
        var lessons = await LessonScope.VisibleAsync(db, scope, ct);
        if (request.FrameworkId is { } f) lessons = lessons.Where(l => l.FrameworkId == f);
        if (request.AreaId is { } a) lessons = lessons.Where(l => l.AreaId == a);
        if (request.AgeBandId is { } b) lessons = lessons.Where(l => l.AgeBandId == b);
        if (request.RoomId is { } r) lessons = lessons.Where(l => l.RoomId == r);

        var page = await lessons.ToCursorPageAsync(request.Cursor, request.Limit, l => new LessonHeader(l.Id, l.CreatedAt), ct);
        var ids = page.Items.Select(h => h.Id).ToList();
        var rows = (await db.Lessons.AsNoTracking().Where(l => ids.Contains(l.Id)).ToListAsync(ct)).ToDictionary(l => l.Id);
        var items = ids.Select(id => LearningMap.ToDto(rows[id])).ToList();

        // Text search runs on the loaded page, because titles are stored per language.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            items = items.Where(l => l.Title.Values.Concat(l.Description is null ? [] : l.Description.Values).Any(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        return Result<CursorPage<LessonDto>>.Success(new CursorPage<LessonDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }

    private sealed record LessonHeader(Guid Id, DateTimeOffset CreatedAt) : ICursorItem;
}

public sealed class GetLessonHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<GetLessonQuery, Result<LessonDto>>
{
    public async Task<Result<LessonDto>> Handle(GetLessonQuery request, CancellationToken ct)
    {
        var row = await (await LessonScope.VisibleAsync(db, scope, ct)).FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        return row is null ? Error.NotFound("Lesson not found.") : Result<LessonDto>.Success(LearningMap.ToDto(row));
    }
}

public sealed class SaveLessonHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<SaveLessonCommand, Result<LessonDto>>
{
    public async Task<Result<LessonDto>> Handle(SaveLessonCommand request, CancellationToken ct)
    {
        if (!await db.CurriculumFrameworks.AnyAsync(f => f.Id == request.FrameworkId, ct))
        {
            return Error.Validation("The framework was not found.", "framework_id");
        }

        if (request.AreaId is { } area && !await db.DevelopmentAreas.AnyAsync(a => a.Id == area && a.FrameworkId == request.FrameworkId, ct))
        {
            return Error.Validation("The area is not part of this framework.", "area_id");
        }

        if (request.AgeBandId is { } band && !await db.AgeBands.AnyAsync(a => a.Id == band && a.FrameworkId == request.FrameworkId, ct))
        {
            return Error.Validation("The age band is not part of this framework.", "age_band_id");
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (request.RoomId is { } room && (!await db.Rooms.AnyAsync(r => r.Id == room, ct) || (rooms is not null && !rooms.Contains(room))))
        {
            return Error.Validation("The room was not found.", "room_id");
        }

        // A teacher may only make lessons for a room of their own, not for the whole centre.
        if (rooms is not null && request.RoomId is null)
        {
            return Error.Forbidden("Choose one of your rooms for this lesson.");
        }

        var milestoneIds = (request.MilestoneIds ?? []).Distinct().ToList();
        if (milestoneIds.Count > 0 && await db.Milestones.CountAsync(m => m.FrameworkId == request.FrameworkId && milestoneIds.Contains(m.Id), ct) != milestoneIds.Count)
        {
            return Error.Validation("One or more milestones are not part of this framework.", "milestone_ids");
        }

        Lesson row;
        if (request.Id is { } id)
        {
            if (!await (await LessonScope.VisibleAsync(db, scope, ct)).AnyAsync(l => l.Id == id, ct))
            {
                return Error.NotFound("Lesson not found.");
            }

            row = await db.Lessons.FirstAsync(l => l.Id == id, ct);
        }
        else
        {
            row = new Lesson();
            db.Lessons.Add(row);
        }

        row.Title = request.Title;
        row.Description = request.Description;
        row.FrameworkId = request.FrameworkId;
        row.AreaId = request.AreaId;
        row.AgeBandId = request.AgeBandId;
        row.RoomId = request.RoomId;
        row.DurationMinutes = request.DurationMinutes;
        row.Materials = (request.Materials ?? []).Select(m => m.Trim()).ToList();
        row.Objectives = (request.Objectives ?? []).Select(o => o.Trim()).ToList();
        row.Links = request.Links ?? [];
        row.MilestoneIds = milestoneIds.Select(m => m.ToString()).ToList();
        await db.SaveChangesAsync(ct);
        return Result<LessonDto>.Success(LearningMap.ToDto(row));
    }
}

public sealed class DeleteLessonHandler(IAppDbContext db, IAccessScope scope) : IRequestHandler<DeleteLessonCommand, Result>
{
    public async Task<Result> Handle(DeleteLessonCommand request, CancellationToken ct)
    {
        var visible = await (await LessonScope.VisibleAsync(db, scope, ct)).AnyAsync(l => l.Id == request.Id, ct);
        if (!visible)
        {
            return Result.Failure(Error.NotFound("Lesson not found."));
        }

        var row = await db.Lessons.FirstAsync(l => l.Id == request.Id, ct);
        db.Lessons.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
