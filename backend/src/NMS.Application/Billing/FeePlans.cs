using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Billing;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed record ComponentInput(Dictionary<string, string> Name, string ComponentType, long AmountFils, decimal? TaxRatePercent);

public sealed record ComponentDto(Guid Id, Dictionary<string, string> Name, string ComponentType, long AmountFils, decimal? TaxRatePercent, int SortOrder);

public sealed record PlanStudentDto(Guid StudentId, string Name, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record FeePlanDto(
    Guid Id,
    Dictionary<string, string> Name,
    Dictionary<string, string>? Description,
    string Status,
    string Period,
    Guid? ProgramId,
    IReadOnlyList<ComponentDto> Components,
    long TotalPerPeriodFils,
    int StudentCount,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record FeePlanDetailDto(FeePlanDto Plan, IReadOnlyList<PlanStudentDto> Students);

public sealed record ListFeePlansQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<FeePlanDto>>>;

public sealed record GetFeePlanQuery(Guid Id) : IRequest<Result<FeePlanDetailDto>>;

public sealed record CreateFeePlanCommand(
    Dictionary<string, string> Name, Dictionary<string, string>? Description, string Period, Guid? ProgramId, string? Status,
    IReadOnlyList<ComponentInput> Components) : IRequest<Result<FeePlanDetailDto>>;

public sealed record UpdateFeePlanCommand(
    Guid Id, Dictionary<string, string> Name, Dictionary<string, string>? Description, string Period, Guid? ProgramId, string Status,
    IReadOnlyList<ComponentInput> Components) : IRequest<Result<FeePlanDetailDto>>;

public sealed record DeleteFeePlanCommand(Guid Id) : IRequest<Result>;

public sealed record AssignStudentsCommand(Guid PlanId, IReadOnlyList<Guid> StudentIds, DateOnly EffectiveFrom) : IRequest<Result<FeePlanDetailDto>>;

public sealed record EndStudentPlanCommand(Guid PlanId, Guid StudentId, DateOnly EffectiveTo) : IRequest<Result<FeePlanDetailDto>>;

public abstract class FeePlanRules<T> : AbstractValidator<T>
    where T : notnull
{
    private static bool ComponentIsValid(ComponentInput c) =>
        c.Name is not null && c.Name.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)
        && ComponentTypes.All.Contains(c.ComponentType) && c.AmountFils is >= 0 and <= 100_000_000
        && (c.TaxRatePercent is null || c.TaxRatePercent is >= 0 and <= 100);

    protected void Apply(
        System.Linq.Expressions.Expression<Func<T, Dictionary<string, string>>> name,
        System.Linq.Expressions.Expression<Func<T, string>> period,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<ComponentInput>>> components)
    {
        RuleFor(name).MustHaveEnglish();
        RuleFor(period).Must(p => FeePeriods.All.Contains(p)).WithMessage("Period must be monthly, termly, annual or one_time.");
        RuleFor(components).NotNull().Must(c => c is { Count: > 0 } && c.Any(x => x.ComponentType != ComponentTypes.Discount))
            .WithMessage("Add at least one fee component.");
        RuleFor(components).Must(list => list is null || list.All(ComponentIsValid))
            .WithMessage("Each component needs an English name, a known type, an amount of 0 or more and a tax rate between 0 and 100.");
    }
}

public sealed class CreateFeePlanValidator : FeePlanRules<CreateFeePlanCommand>
{
    public CreateFeePlanValidator()
    {
        Apply(c => c.Name, c => c.Period, c => c.Components);
        RuleFor(c => c.Status!).Must(s => s is "active" or "inactive").When(c => c.Status is not null).WithMessage("Status must be active or inactive.");
    }
}

public sealed class UpdateFeePlanValidator : FeePlanRules<UpdateFeePlanCommand>
{
    public UpdateFeePlanValidator()
    {
        Apply(c => c.Name, c => c.Period, c => c.Components);
        RuleFor(c => c.Status).Must(s => s is "active" or "inactive").WithMessage("Status must be active or inactive.");
    }
}

internal static class PlanMapping
{
    public static FeePlanDto ToDto(FeePlan p, int students) => new(
        p.Id, p.Name, p.Description, p.Status, p.Period, p.ProgramId,
        p.Components.Where(c => !c.IsDeleted).OrderBy(c => c.SortOrder)
            .Select(c => new ComponentDto(c.Id, c.Name, c.ComponentType, c.AmountFils, c.TaxRatePercent, c.SortOrder)).ToList(),
        p.Components.Where(c => !c.IsDeleted && c.ComponentType != ComponentTypes.Discount).Sum(c => c.AmountFils)
            - p.Components.Where(c => !c.IsDeleted && c.ComponentType == ComponentTypes.Discount).Sum(c => c.AmountFils),
        students, p.CreatedAt);

    public static async Task<FeePlanDetailDto> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var plan = await db.FeePlans.AsNoTracking().Include(p => p.Components).FirstAsync(p => p.Id == id, ct);
        var students = await db.StudentFeePlans.AsNoTracking().Where(s => s.FeePlanId == id)
            .OrderBy(s => s.EffectiveFrom)
            .Select(s => new PlanStudentDto(s.StudentId, db.Students.Where(x => x.Id == s.StudentId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault() ?? "", s.EffectiveFrom, s.EffectiveTo))
            .ToListAsync(ct);
        var current = students.Count(s => s.EffectiveTo is null);
        return new FeePlanDetailDto(ToDto(plan, current), students);
    }

    public static void ReplaceComponents(IAppDbContext db, FeePlan plan, IReadOnlyList<ComponentInput> inputs, IClock clock)
    {
        foreach (var old in plan.Components.Where(c => !c.IsDeleted))
        {
            old.IsDeleted = true;
            old.DeletedAt = clock.UtcNow;
        }

        var order = 0;
        foreach (var input in inputs)
        {
            var component = new FeeComponent
            {
                FeePlanId = plan.Id, Name = input.Name, ComponentType = input.ComponentType, AmountFils = input.AmountFils,
                TaxRatePercent = input.ComponentType == ComponentTypes.Discount ? null : input.TaxRatePercent, SortOrder = order++,
            };
            plan.Components.Add(component);
            db.FeeComponents.Add(component);
        }
    }
}

public sealed class ListFeePlansHandler(IAppDbContext db) : IRequestHandler<ListFeePlansQuery, Result<CursorPage<FeePlanDto>>>
{
    public async Task<Result<CursorPage<FeePlanDto>>> Handle(ListFeePlansQuery request, CancellationToken ct)
    {
        var page = await db.FeePlans.AsNoTracking().Include(p => p.Components).ToCursorPageAsync(
            request.Cursor, request.Limit,
            p => new FeePlanHeader(p.Id, p.CreatedAt), ct);
        var ids = page.Items.Select(i => i.Id).ToList();
        var plans = await db.FeePlans.AsNoTracking().Include(p => p.Components).Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var counts = await db.StudentFeePlans.AsNoTracking().Where(s => ids.Contains(s.FeePlanId) && s.EffectiveTo == null)
            .GroupBy(s => s.FeePlanId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var items = ids.Select(id => PlanMapping.ToDto(plans.First(p => p.Id == id), counts.GetValueOrDefault(id))).ToList();
        return Result<CursorPage<FeePlanDto>>.Success(new CursorPage<FeePlanDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }

    private sealed record FeePlanHeader(Guid Id, DateTimeOffset CreatedAt) : ICursorItem;
}

public sealed class GetFeePlanHandler(IAppDbContext db) : IRequestHandler<GetFeePlanQuery, Result<FeePlanDetailDto>>
{
    public async Task<Result<FeePlanDetailDto>> Handle(GetFeePlanQuery request, CancellationToken ct) =>
        await db.FeePlans.AnyAsync(p => p.Id == request.Id, ct)
            ? Result<FeePlanDetailDto>.Success(await PlanMapping.DetailAsync(db, request.Id, ct))
            : Error.NotFound("Fee plan not found.");
}

public sealed class CreateFeePlanHandler(IAppDbContext db, IClock clock) : IRequestHandler<CreateFeePlanCommand, Result<FeePlanDetailDto>>
{
    public async Task<Result<FeePlanDetailDto>> Handle(CreateFeePlanCommand request, CancellationToken ct)
    {
        if (request.ProgramId is { } programId && !await db.Programs.AnyAsync(p => p.Id == programId, ct))
        {
            return Error.Validation("The selected programme does not exist.", "program_id");
        }

        var existing = await db.FeePlans.AsNoTracking().Select(p => p.Name).ToListAsync(ct);
        if (NameConflict.Exists(existing, request.Name["en"]))
        {
            return Error.Conflict("A fee plan with this name already exists.", "name");
        }

        var plan = new FeePlan
        {
            Name = request.Name, Description = request.Description, Period = request.Period, ProgramId = request.ProgramId,
            Status = request.Status ?? "active",
        };
        db.FeePlans.Add(plan);
        PlanMapping.ReplaceComponents(db, plan, request.Components, clock);
        await db.SaveChangesAsync(ct);
        return Result<FeePlanDetailDto>.Success(await PlanMapping.DetailAsync(db, plan.Id, ct));
    }
}

public sealed class UpdateFeePlanHandler(IAppDbContext db, IClock clock) : IRequestHandler<UpdateFeePlanCommand, Result<FeePlanDetailDto>>
{
    public async Task<Result<FeePlanDetailDto>> Handle(UpdateFeePlanCommand request, CancellationToken ct)
    {
        var plan = await db.FeePlans.Include(p => p.Components).FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (plan is null)
        {
            return Error.NotFound("Fee plan not found.");
        }

        if (request.ProgramId is { } programId && !await db.Programs.AnyAsync(p => p.Id == programId, ct))
        {
            return Error.Validation("The selected programme does not exist.", "program_id");
        }

        var others = await db.FeePlans.AsNoTracking().Where(p => p.Id != request.Id).Select(p => p.Name).ToListAsync(ct);
        if (NameConflict.Exists(others, request.Name["en"]))
        {
            return Error.Conflict("A fee plan with this name already exists.", "name");
        }

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.Period = request.Period;
        plan.ProgramId = request.ProgramId;
        plan.Status = request.Status;
        PlanMapping.ReplaceComponents(db, plan, request.Components, clock);
        await db.SaveChangesAsync(ct);
        return Result<FeePlanDetailDto>.Success(await PlanMapping.DetailAsync(db, plan.Id, ct));
    }
}

public sealed class DeleteFeePlanHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteFeePlanCommand, Result>
{
    public async Task<Result> Handle(DeleteFeePlanCommand request, CancellationToken ct)
    {
        var plan = await db.FeePlans.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (plan is null)
        {
            return Result.Failure(Error.NotFound("Fee plan not found."));
        }

        if (await db.Invoices.AnyAsync(i => i.FeePlanId == plan.Id, ct))
        {
            return Result.Failure(Error.Conflict("This plan has invoices. Set it to inactive instead of removing it."));
        }

        plan.IsDeleted = true;
        plan.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class AssignStudentsHandler(IAppDbContext db) : IRequestHandler<AssignStudentsCommand, Result<FeePlanDetailDto>>
{
    public async Task<Result<FeePlanDetailDto>> Handle(AssignStudentsCommand request, CancellationToken ct)
    {
        if (!await db.FeePlans.AnyAsync(p => p.Id == request.PlanId, ct))
        {
            return Error.NotFound("Fee plan not found.");
        }

        var ids = request.StudentIds.Distinct().ToList();
        if (ids.Count == 0 || await db.Students.CountAsync(s => ids.Contains(s.Id), ct) != ids.Count)
        {
            return Error.Validation("Choose existing children.", "student_ids");
        }

        foreach (var id in ids)
        {
            var already = await db.StudentFeePlans.AnyAsync(s => s.StudentId == id && s.FeePlanId == request.PlanId && s.EffectiveTo == null, ct);
            if (!already)
            {
                db.StudentFeePlans.Add(new StudentFeePlan { StudentId = id, FeePlanId = request.PlanId, EffectiveFrom = request.EffectiveFrom });
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<FeePlanDetailDto>.Success(await PlanMapping.DetailAsync(db, request.PlanId, ct));
    }
}

public sealed class EndStudentPlanHandler(IAppDbContext db) : IRequestHandler<EndStudentPlanCommand, Result<FeePlanDetailDto>>
{
    public async Task<Result<FeePlanDetailDto>> Handle(EndStudentPlanCommand request, CancellationToken ct)
    {
        var link = await db.StudentFeePlans.FirstOrDefaultAsync(s => s.FeePlanId == request.PlanId && s.StudentId == request.StudentId && s.EffectiveTo == null, ct);
        if (link is null)
        {
            return Error.NotFound("This child is not on the plan.");
        }

        if (request.EffectiveTo < link.EffectiveFrom)
        {
            return Error.Validation("The end date cannot be before the start date.", "effective_to");
        }

        link.EffectiveTo = request.EffectiveTo;
        await db.SaveChangesAsync(ct);
        return Result<FeePlanDetailDto>.Success(await PlanMapping.DetailAsync(db, request.PlanId, ct));
    }
}
