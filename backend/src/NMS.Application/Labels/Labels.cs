using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Labels;

public sealed record LabelDto(Guid Id, Dictionary<string, string> Name, string Color, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListLabelsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<LabelDto>>>;

public sealed record CreateLabelCommand(Dictionary<string, string> Name, string Color) : IRequest<Result<LabelDto>>;

public sealed record UpdateLabelCommand(Guid Id, Dictionary<string, string> Name, string Color) : IRequest<Result<LabelDto>>;

public sealed record DeleteLabelCommand(Guid Id) : IRequest<Result>;

internal static class LabelRules
{
    public static IRuleBuilderOptions<T, string> HexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Colour must be a hex value such as #E07A5F.");
}

public sealed class CreateLabelValidator : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.Color).HexColor();
    }
}

public sealed class UpdateLabelValidator : AbstractValidator<UpdateLabelCommand>
{
    public UpdateLabelValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.Color).HexColor();
    }
}

public sealed class ListLabelsHandler(IAppDbContext db) : IRequestHandler<ListLabelsQuery, Result<CursorPage<LabelDto>>>
{
    public async Task<Result<CursorPage<LabelDto>>> Handle(ListLabelsQuery request, CancellationToken ct)
    {
        var page = await db.Labels.AsNoTracking().ToCursorPageAsync(
            request.Cursor, request.Limit, l => new LabelDto(l.Id, l.Name, l.Color, l.CreatedAt), ct);
        return Result<CursorPage<LabelDto>>.Success(page);
    }
}

public sealed class CreateLabelHandler(IAppDbContext db) : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    public async Task<Result<LabelDto>> Handle(CreateLabelCommand request, CancellationToken ct)
    {
        var names = await db.Labels.AsNoTracking().Select(l => l.Name).ToListAsync(ct);
        if (NameConflict.Exists(names, request.Name["en"]))
        {
            return Error.Conflict("A label with this name already exists.", "name");
        }

        var label = new Label { Name = request.Name, Color = request.Color };
        db.Labels.Add(label);
        await db.SaveChangesAsync(ct);
        return Result<LabelDto>.Success(new LabelDto(label.Id, label.Name, label.Color, label.CreatedAt));
    }
}

public sealed class UpdateLabelHandler(IAppDbContext db) : IRequestHandler<UpdateLabelCommand, Result<LabelDto>>
{
    public async Task<Result<LabelDto>> Handle(UpdateLabelCommand request, CancellationToken ct)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (label is null)
        {
            return Error.NotFound("Label not found.");
        }

        var names = await db.Labels.AsNoTracking().Where(l => l.Id != request.Id).Select(l => l.Name).ToListAsync(ct);
        if (NameConflict.Exists(names, request.Name["en"]))
        {
            return Error.Conflict("A label with this name already exists.", "name");
        }

        label.Name = request.Name;
        label.Color = request.Color;
        await db.SaveChangesAsync(ct);
        return Result<LabelDto>.Success(new LabelDto(label.Id, label.Name, label.Color, label.CreatedAt));
    }
}

public sealed class DeleteLabelHandler(IAppDbContext db) : IRequestHandler<DeleteLabelCommand, Result>
{
    public async Task<Result> Handle(DeleteLabelCommand request, CancellationToken ct)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (label is null)
        {
            return Result.Failure(Error.NotFound("Label not found."));
        }

        var links = await db.StudentLabels.Where(sl => sl.LabelId == label.Id).ToListAsync(ct);
        db.StudentLabels.RemoveRange(links);
        db.Labels.Remove(label);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
