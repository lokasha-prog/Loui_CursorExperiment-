using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Notifications;
using NMS.Application.Security;
using NMS.Domain.Forms;
using NMS.Domain.Notifications;
using NMS.Shared.Results;

namespace NMS.Application.Forms;

public sealed record StepDto(Guid Id, Dictionary<string, string> Name, int SortOrder, Guid? FormId, string? FormName, int DueDays);

public sealed record StudentStepDto(Guid StepId, Dictionary<string, string> Name, Guid? FormId, string Status, DateOnly? DueDate, DateTimeOffset? CompletedAt, bool Overdue);

public sealed record StudentAdmissionDto(Guid StudentId, string StudentName, int Done, int Total, IReadOnlyList<StudentStepDto> Steps);

public sealed record StepStatusRow(Guid StepId, Dictionary<string, string> Name, int Current, int Overdue, int Done, int Total, DateOnly? NextDue);

public sealed record ChildProgressRow(Guid StudentId, string StudentName, int Done, int Total, string? CurrentStep, DateOnly? DueDate, bool Overdue);

public sealed record AdmissionStatusDto(IReadOnlyList<StepStatusRow> Steps, IReadOnlyList<ChildProgressRow> Children);

public sealed record ListStepsQuery : IRequest<Result<IReadOnlyList<StepDto>>>;

public sealed record SaveStepCommand(Guid? Id, Dictionary<string, string> Name, Guid? FormId, int DueDays) : IRequest<Result<IReadOnlyList<StepDto>>>;

public sealed record DeleteStepCommand(Guid Id) : IRequest<Result<IReadOnlyList<StepDto>>>;

public sealed record ReorderStepsCommand(IReadOnlyList<Guid> OrderedIds) : IRequest<Result<IReadOnlyList<StepDto>>>;

public sealed record StartAdmissionCommand(Guid StudentId) : IRequest<Result<StudentAdmissionDto>>;

public sealed record GetStudentAdmissionQuery(Guid StudentId) : IRequest<Result<StudentAdmissionDto>>;

public sealed record StepActionCommand(Guid StudentId, Guid StepId, string Action) : IRequest<Result<StudentAdmissionDto>>;

public sealed record AdmissionStatusQuery : IRequest<Result<AdmissionStatusDto>>;

public sealed record RunAdmissionRemindersCommand : IRequest<Result<int>>;

public sealed class SaveStepValidator : AbstractValidator<SaveStepCommand>
{
    public SaveStepValidator()
    {
        RuleFor(x => x.Name).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 150).WithMessage("Give the step an English name (up to 150 characters).");
        RuleFor(x => x.DueDays).InclusiveBetween(0, 365);
    }
}

public sealed class StepActionValidator : AbstractValidator<StepActionCommand>
{
    public StepActionValidator() => RuleFor(x => x.Action).Must(a => a is "complete" or "skip").WithMessage("Action must be complete or skip.");
}

/// <summary>Moves a child along the admission steps and tells parents when a new step (with its form) begins.</summary>
public sealed class AdmissionService(IAppDbContext db, CentreCalendar calendar, Notifier notifier, IClock clock)
{
    public async Task EnterAsync(StudentAdmissionStep row, AdmissionStep step, CancellationToken ct)
    {
        row.Status = AdmissionStatuses.Current;
        row.DueDate = calendar.Today.AddDays(step.DueDays);
        if (step.FormId is null)
        {
            return;
        }

        var form = await db.Forms.AsNoTracking().Where(f => f.Id == step.FormId).Select(f => f.Name).FirstOrDefaultAsync(ct);
        var name = await db.Students.AsNoTracking().Where(s => s.Id == row.StudentId).Select(s => s.FirstName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var users = await ParentUsersAsync(row.StudentId, ct);
        await notifier.NotifyAsync(users, NotificationTiers.Operational, "admission_form",
            new() { ["en"] = $"Please complete: {form?.GetValueOrDefault("en") ?? step.Name["en"]}", ["ar"] = $"يرجى إكمال: {form?.GetValueOrDefault("ar") ?? form?.GetValueOrDefault("en") ?? step.Name.GetValueOrDefault("ar") ?? step.Name["en"]}" },
            new() { ["en"] = $"A form is waiting for {name} (due {row.DueDate:yyyy-MM-dd}).", ["ar"] = $"هناك نموذج بانتظار {name} (الاستحقاق {row.DueDate:yyyy-MM-dd})." }, $"/forms?fill={step.FormId}&student={row.StudentId}", ct);
    }

    /// <summary>Closes the current step and starts the next one, if there is one.</summary>
    public async Task FinishAsync(Guid studentId, StudentAdmissionStep row, string status, Guid? submissionId, CancellationToken ct)
    {
        row.Status = status;
        row.CompletedAt = clock.UtcNow;
        row.SubmissionId = submissionId;
        var order = await db.AdmissionSteps.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.SortOrder, ct);
        var mine = order.GetValueOrDefault(row.StepId);
        var next = (await db.StudentAdmissionSteps.Where(r => r.StudentId == studentId && r.Status == AdmissionStatuses.Pending).ToListAsync(ct))
            .Where(r => order.ContainsKey(r.StepId)).OrderBy(r => order[r.StepId]).FirstOrDefault(r => order[r.StepId] >= mine);
        if (next is not null)
        {
            await EnterAsync(next, await db.AdmissionSteps.AsNoTracking().FirstAsync(s => s.Id == next.StepId, ct), ct);
        }
    }

    /// <summary>When a parent submits the form a step is waiting for, that step is done.</summary>
    public async Task OnFormSubmittedAsync(Guid studentId, Guid formId, Guid submissionId, CancellationToken ct)
    {
        var current = await db.StudentAdmissionSteps.Where(r => r.StudentId == studentId && r.Status == AdmissionStatuses.Current && db.AdmissionSteps.Any(s => s.Id == r.StepId && s.FormId == formId)).FirstOrDefaultAsync(ct);
        if (current is not null)
        {
            await FinishAsync(studentId, current, AdmissionStatuses.Done, submissionId, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<Guid>> ParentUsersAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentGuardians.AsNoTracking()
            .Where(sg => sg.StudentId == studentId && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);

    public async Task<StudentAdmissionDto> ViewAsync(Guid studentId, CancellationToken ct)
    {
        var today = calendar.Today;
        var name = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var steps = await db.AdmissionSteps.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var rows = (await db.StudentAdmissionSteps.AsNoTracking().Where(r => r.StudentId == studentId).ToListAsync(ct)).Where(r => steps.ContainsKey(r.StepId)).OrderBy(r => steps[r.StepId].SortOrder).ToList();
        var list = rows.Select(r => new StudentStepDto(r.StepId, steps[r.StepId].Name, steps[r.StepId].FormId, r.Status, r.DueDate, r.CompletedAt, r.Status == AdmissionStatuses.Current && r.DueDate < today)).ToList();
        return new StudentAdmissionDto(studentId, name, list.Count(s => s.Status is AdmissionStatuses.Done or AdmissionStatuses.Skipped), list.Count, list);
    }
}

public sealed class ListStepsHandler(IAppDbContext db) : IRequestHandler<ListStepsQuery, Result<IReadOnlyList<StepDto>>>
{
    public async Task<Result<IReadOnlyList<StepDto>>> Handle(ListStepsQuery request, CancellationToken ct) => Result<IReadOnlyList<StepDto>>.Success(await StepList.LoadAsync(db, ct));
}

internal static class StepList
{
    public static async Task<IReadOnlyList<StepDto>> LoadAsync(IAppDbContext db, CancellationToken ct)
    {
        var steps = await db.AdmissionSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        var formIds = steps.Where(s => s.FormId != null).Select(s => s.FormId!.Value).ToList();
        var forms = await db.Forms.AsNoTracking().Where(f => formIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);
        return steps.Select(s => new StepDto(s.Id, s.Name, s.SortOrder, s.FormId, s.FormId is { } f ? forms.GetValueOrDefault(f)?.GetValueOrDefault("en") : null, s.DueDays)).ToList();
    }
}

public sealed class SaveStepHandler(IAppDbContext db) : IRequestHandler<SaveStepCommand, Result<IReadOnlyList<StepDto>>>
{
    public async Task<Result<IReadOnlyList<StepDto>>> Handle(SaveStepCommand request, CancellationToken ct)
    {
        if (request.FormId is { } formId && !await db.Forms.AnyAsync(f => f.Id == formId, ct))
        {
            return Error.Validation("The form does not exist.", "form_id");
        }

        AdmissionStep step;
        if (request.Id is { } id)
        {
            var found = await db.AdmissionSteps.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Step not found.");
            }

            step = found;
        }
        else
        {
            step = new AdmissionStep { SortOrder = (await db.AdmissionSteps.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0) + 1 };
            db.AdmissionSteps.Add(step);
        }

        step.Name = request.Name.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary(n => n.Key, n => n.Value.Trim());
        step.FormId = request.FormId;
        step.DueDays = request.DueDays;
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<StepDto>>.Success(await StepList.LoadAsync(db, ct));
    }
}

public sealed class DeleteStepHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteStepCommand, Result<IReadOnlyList<StepDto>>>
{
    public async Task<Result<IReadOnlyList<StepDto>>> Handle(DeleteStepCommand request, CancellationToken ct)
    {
        var step = await db.AdmissionSteps.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (step is null)
        {
            return Error.NotFound("Step not found.");
        }

        if (await db.StudentAdmissionSteps.AnyAsync(r => r.StepId == step.Id && r.Status != AdmissionStatuses.Done, ct))
        {
            return Error.Conflict("Children are still working through this step. Finish or skip it for them first.");
        }

        step.IsDeleted = true;
        step.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<StepDto>>.Success(await StepList.LoadAsync(db, ct));
    }
}

public sealed class ReorderStepsHandler(IAppDbContext db) : IRequestHandler<ReorderStepsCommand, Result<IReadOnlyList<StepDto>>>
{
    public async Task<Result<IReadOnlyList<StepDto>>> Handle(ReorderStepsCommand request, CancellationToken ct)
    {
        var steps = await db.AdmissionSteps.ToListAsync(ct);
        if (request.OrderedIds.Count != steps.Count || request.OrderedIds.Distinct().Count() != steps.Count || request.OrderedIds.Any(i => steps.All(s => s.Id != i)))
        {
            return Error.Validation("List every step exactly once.", "ordered_ids");
        }

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            steps.First(s => s.Id == request.OrderedIds[i]).SortOrder = i + 1;
        }

        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<StepDto>>.Success(await StepList.LoadAsync(db, ct));
    }
}

public sealed class StartAdmissionHandler(IAppDbContext db, AdmissionService service) : IRequestHandler<StartAdmissionCommand, Result<StudentAdmissionDto>>
{
    public async Task<Result<StudentAdmissionDto>> Handle(StartAdmissionCommand request, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId, ct))
        {
            return Error.NotFound("Child not found.");
        }

        var steps = await db.AdmissionSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        if (steps.Count == 0)
        {
            return Error.Validation("Set up the admission steps first.", "steps");
        }

        var present = (await db.StudentAdmissionSteps.AsNoTracking().Where(r => r.StudentId == request.StudentId).Select(r => r.StepId).ToListAsync(ct)).ToHashSet();
        if (steps.All(s => present.Contains(s.Id)))
        {
            return Error.InvalidState("This child has already started the admission process.");
        }

        var added = steps.Where(s => !present.Contains(s.Id)).Select(s => new StudentAdmissionStep { StudentId = request.StudentId, StepId = s.Id }).ToList();
        db.StudentAdmissionSteps.AddRange(added);
        var hasCurrent = await db.StudentAdmissionSteps.AnyAsync(r => r.StudentId == request.StudentId && r.Status == AdmissionStatuses.Current, ct);
        if (!hasCurrent)
        {
            var first = added.First();
            await service.EnterAsync(first, steps.First(s => s.Id == first.StepId), ct);
        }

        await db.SaveChangesAsync(ct);
        return Result<StudentAdmissionDto>.Success(await service.ViewAsync(request.StudentId, ct));
    }
}

public sealed class GetStudentAdmissionHandler(IAppDbContext db, IAccessScope scope, AdmissionService service) : IRequestHandler<GetStudentAdmissionQuery, Result<StudentAdmissionDto>>
{
    public async Task<Result<StudentAdmissionDto>> Handle(GetStudentAdmissionQuery request, CancellationToken ct)
    {
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null ? !own.Contains(request.StudentId) : !await db.Students.AnyAsync(s => s.Id == request.StudentId, ct))
        {
            return Error.NotFound("Child not found.");
        }

        return Result<StudentAdmissionDto>.Success(await service.ViewAsync(request.StudentId, ct));
    }
}

public sealed class StepActionHandler(IAppDbContext db, AdmissionService service) : IRequestHandler<StepActionCommand, Result<StudentAdmissionDto>>
{
    public async Task<Result<StudentAdmissionDto>> Handle(StepActionCommand request, CancellationToken ct)
    {
        var row = await db.StudentAdmissionSteps.FirstOrDefaultAsync(r => r.StudentId == request.StudentId && r.StepId == request.StepId, ct);
        if (row is null)
        {
            return Error.NotFound("This child is not on that step.");
        }

        if (row.Status is AdmissionStatuses.Done or AdmissionStatuses.Skipped)
        {
            return Error.InvalidState("This step is already finished.");
        }

        await service.FinishAsync(request.StudentId, row, request.Action == "skip" ? AdmissionStatuses.Skipped : AdmissionStatuses.Done, null, ct);
        await db.SaveChangesAsync(ct);
        return Result<StudentAdmissionDto>.Success(await service.ViewAsync(request.StudentId, ct));
    }
}

/// <summary>The admission dashboard (spec 03 §12.3): per step how many children are on it, late, or done; and each child's progress.</summary>
public sealed class AdmissionStatusHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<AdmissionStatusQuery, Result<AdmissionStatusDto>>
{
    public async Task<Result<AdmissionStatusDto>> Handle(AdmissionStatusQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var steps = await db.AdmissionSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        var rows = await db.StudentAdmissionSteps.AsNoTracking().ToListAsync(ct);
        var perStep = steps.Select(s =>
        {
            var mine = rows.Where(r => r.StepId == s.Id).ToList();
            var current = mine.Where(r => r.Status == AdmissionStatuses.Current).ToList();
            return new StepStatusRow(s.Id, s.Name, current.Count, current.Count(r => r.DueDate < today), mine.Count(r => r.Status is AdmissionStatuses.Done or AdmissionStatuses.Skipped), mine.Count, current.Select(r => r.DueDate).Min());
        }).ToList();

        var kidIds = rows.Select(r => r.StudentId).Distinct().ToList();
        var names = await db.Students.AsNoTracking().Where(s => kidIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FirstName + " " + s.LastName, ct);
        var children = kidIds.Select(id =>
        {
            var mine = rows.Where(r => r.StudentId == id && steps.Any(s => s.Id == r.StepId)).ToList();
            var current = mine.FirstOrDefault(r => r.Status == AdmissionStatuses.Current);
            return new ChildProgressRow(id, names.GetValueOrDefault(id, string.Empty), mine.Count(r => r.Status is AdmissionStatuses.Done or AdmissionStatuses.Skipped), mine.Count,
                current is null ? null : steps.First(s => s.Id == current.StepId).Name.GetValueOrDefault("en"), current?.DueDate, current?.DueDate < today);
        }).OrderByDescending(c => c.Overdue).ThenBy(c => c.StudentName).ToList();
        return Result<AdmissionStatusDto>.Success(new AdmissionStatusDto(perStep, children));
    }
}

/// <summary>Daily run: when a step passes its due date, tell the parents and the managers once.</summary>
public sealed class RunAdmissionRemindersHandler(IAppDbContext db, CentreCalendar calendar, Notifier notifier, AdmissionService service, IClock clock) : IRequestHandler<RunAdmissionRemindersCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunAdmissionRemindersCommand request, CancellationToken ct)
    {
        var today = calendar.Today;
        var late = await db.StudentAdmissionSteps.Where(r => r.Status == AdmissionStatuses.Current && r.DueDate < today && r.OverdueNotifiedAt == null).ToListAsync(ct);
        if (late.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var managers = await notifier.UsersWithPermissionAsync(Permissions.FormsWrite, ct);
        var steps = await db.AdmissionSteps.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        foreach (var row in late)
        {
            if (!steps.TryGetValue(row.StepId, out var step))
            {
                continue;
            }

            var child = await db.Students.AsNoTracking().Where(s => s.Id == row.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) ?? string.Empty;
            var parents = await service.ParentUsersAsync(row.StudentId, ct);
            await notifier.NotifyAsync(parents, NotificationTiers.Operational, "admission_overdue",
                new() { ["en"] = $"Admission step overdue: {step.Name["en"]}", ["ar"] = $"خطوة التسجيل متأخرة: {step.Name.GetValueOrDefault("ar") ?? step.Name["en"]}" },
                new() { ["en"] = $"{child}: please complete this step. It was due {row.DueDate:yyyy-MM-dd}.", ["ar"] = $"{child}: يرجى إكمال هذه الخطوة. كان موعدها {row.DueDate:yyyy-MM-dd}." }, "/forms", ct);
            await notifier.NotifyAsync(managers, NotificationTiers.Operational, "admission_overdue_staff",
                new() { ["en"] = $"{child} is late on: {step.Name["en"]}", ["ar"] = $"{child} متأخر في: {step.Name.GetValueOrDefault("ar") ?? step.Name["en"]}" },
                new() { ["en"] = $"Due {row.DueDate:yyyy-MM-dd}.", ["ar"] = $"الاستحقاق {row.DueDate:yyyy-MM-dd}." }, "/forms", ct);
            row.OverdueNotifiedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(late.Count);
    }
}
