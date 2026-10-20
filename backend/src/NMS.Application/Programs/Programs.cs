using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Programs;

public sealed record ProgramDto(
    Guid Id,
    Dictionary<string, string> Name,
    Dictionary<string, string>? Description,
    int DaysPerWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string AgeGroup,
    bool IsActive,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListProgramsQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<ProgramDto>>>;

public sealed record CreateProgramCommand(
    Dictionary<string, string> Name,
    Dictionary<string, string>? Description,
    int DaysPerWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string AgeGroup,
    bool IsActive = true) : IRequest<Result<ProgramDto>>;

public sealed record UpdateProgramCommand(
    Guid Id,
    Dictionary<string, string> Name,
    Dictionary<string, string>? Description,
    int DaysPerWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string AgeGroup,
    bool IsActive) : IRequest<Result<ProgramDto>>;

public sealed record DeleteProgramCommand(Guid Id) : IRequest<Result>;

public sealed class CreateProgramValidator : AbstractValidator<CreateProgramCommand>
{
    public CreateProgramValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.DaysPerWeek).InclusiveBetween(1, 7);
        RuleFor(c => c.EndTime).GreaterThan(c => c.StartTime).WithMessage("End time must be after start time.");
        RuleFor(c => c.AgeGroup).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateProgramValidator : AbstractValidator<UpdateProgramCommand>
{
    public UpdateProgramValidator()
    {
        RuleFor(c => c.Name).MustHaveEnglish();
        RuleFor(c => c.DaysPerWeek).InclusiveBetween(1, 7);
        RuleFor(c => c.EndTime).GreaterThan(c => c.StartTime).WithMessage("End time must be after start time.");
        RuleFor(c => c.AgeGroup).NotEmpty().MaximumLength(100);
    }
}

internal static class ProgramMapping
{
    public static ProgramDto ToDto(Program p) => new(
        p.Id, p.Name, p.Description, p.DaysPerWeek, p.StartTime, p.EndTime, p.AgeGroup, p.IsActive, p.CreatedAt);
}

public sealed class ListProgramsHandler(IAppDbContext db)
    : IRequestHandler<ListProgramsQuery, Result<CursorPage<ProgramDto>>>
{
    public async Task<Result<CursorPage<ProgramDto>>> Handle(ListProgramsQuery request, CancellationToken ct)
    {
        var page = await db.Programs.AsNoTracking().ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            p => new ProgramDto(
                p.Id, p.Name, p.Description, p.DaysPerWeek, p.StartTime, p.EndTime, p.AgeGroup, p.IsActive, p.CreatedAt),
            ct);
        return Result<CursorPage<ProgramDto>>.Success(page);
    }
}

public sealed class CreateProgramHandler(IAppDbContext db) : IRequestHandler<CreateProgramCommand, Result<ProgramDto>>
{
    public async Task<Result<ProgramDto>> Handle(CreateProgramCommand request, CancellationToken ct)
    {
        var names = await db.Programs.AsNoTracking().Select(p => p.Name).ToListAsync(ct);
        if (NameConflict.Exists(names, request.Name["en"]))
        {
            return Error.Conflict("A program with this name already exists.", "name");
        }

        var program = new Program
        {
            Name = request.Name,
            Description = request.Description,
            DaysPerWeek = request.DaysPerWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            AgeGroup = request.AgeGroup,
            IsActive = request.IsActive,
        };
        db.Programs.Add(program);
        await db.SaveChangesAsync(ct);
        return Result<ProgramDto>.Success(ProgramMapping.ToDto(program));
    }
}

public sealed class UpdateProgramHandler(IAppDbContext db) : IRequestHandler<UpdateProgramCommand, Result<ProgramDto>>
{
    public async Task<Result<ProgramDto>> Handle(UpdateProgramCommand request, CancellationToken ct)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (program is null)
        {
            return Error.NotFound("Program not found.");
        }

        var names = await db.Programs.AsNoTracking().Where(p => p.Id != request.Id).Select(p => p.Name).ToListAsync(ct);
        if (NameConflict.Exists(names, request.Name["en"]))
        {
            return Error.Conflict("A program with this name already exists.", "name");
        }

        program.Name = request.Name;
        program.Description = request.Description;
        program.DaysPerWeek = request.DaysPerWeek;
        program.StartTime = request.StartTime;
        program.EndTime = request.EndTime;
        program.AgeGroup = request.AgeGroup;
        program.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<ProgramDto>.Success(ProgramMapping.ToDto(program));
    }
}

public sealed class DeleteProgramHandler(IAppDbContext db) : IRequestHandler<DeleteProgramCommand, Result>
{
    public async Task<Result> Handle(DeleteProgramCommand request, CancellationToken ct)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (program is null)
        {
            return Result.Failure(Error.NotFound("Program not found."));
        }

        if (await db.Students.AnyAsync(s => s.ProgramId == program.Id, ct))
        {
            return Result.Failure(Error.InvalidState("Move the students out of this program before deleting it."));
        }

        db.Programs.Remove(program);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
