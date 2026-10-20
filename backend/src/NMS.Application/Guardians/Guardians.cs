using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Guardians;

public sealed record GuardianDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Relationship,
    bool IsPrimary,
    bool CanPickup,
    bool ReceivesCommunications,
    bool HasLogin);

public sealed record GuardianFields(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Relationship,
    bool IsPrimary,
    bool CanPickup = true,
    bool ReceivesCommunications = true);

public sealed record ListGuardiansQuery(Guid StudentId) : IRequest<Result<IReadOnlyList<GuardianDto>>>;

public sealed record AddGuardianCommand(Guid StudentId, GuardianFields Guardian) : IRequest<Result<GuardianDto>>;

public sealed record UpdateGuardianCommand(Guid StudentId, Guid GuardianId, GuardianFields Guardian)
    : IRequest<Result<GuardianDto>>;

public sealed record RemoveGuardianCommand(Guid StudentId, Guid GuardianId) : IRequest<Result>;

public sealed class GuardianFieldsValidator : AbstractValidator<GuardianFields>
{
    public GuardianFieldsValidator()
    {
        RuleFor(g => g.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(g => g.LastName).NotEmpty().MaximumLength(100);
        RuleFor(g => g.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(g => g.Relationship).Must(r => GuardianRelationships.All.Contains(r)).WithMessage("Unknown relationship.");
    }
}

public sealed class AddGuardianValidator : AbstractValidator<AddGuardianCommand>
{
    public AddGuardianValidator() => RuleFor(c => c.Guardian).NotNull().SetValidator(new GuardianFieldsValidator());
}

public sealed class UpdateGuardianValidator : AbstractValidator<UpdateGuardianCommand>
{
    public UpdateGuardianValidator() => RuleFor(c => c.Guardian).NotNull().SetValidator(new GuardianFieldsValidator());
}

internal static class GuardianAccess
{
    /// <summary>Same visibility rules as the student itself, so a parent can only reach their own child's guardians.</summary>
    public static async Task<bool> CanSeeStudentAsync(
        IAppDbContext db, IAccessScope scope, Guid studentId, CancellationToken ct)
    {
        var students = db.Students.AsNoTracking().Where(s => s.Id == studentId);
        var own = await scope.GetOwnStudentIdsAsync(ct);
        if (own is not null)
        {
            students = students.Where(s => own.Contains(s.Id));
        }

        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            students = students.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
        }

        return await students.AnyAsync(ct);
    }

    public static GuardianDto ToDto(StudentGuardian link) => new(
        link.Guardian!.Id, link.Guardian.FirstName, link.Guardian.LastName, link.Guardian.Email, link.Guardian.Phone,
        link.Guardian.Relationship, link.IsPrimary, link.Guardian.CanPickup, link.Guardian.ReceivesCommunications,
        link.Guardian.AppUserId is not null);
}

public sealed class ListGuardiansHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<ListGuardiansQuery, Result<IReadOnlyList<GuardianDto>>>
{
    public async Task<Result<IReadOnlyList<GuardianDto>>> Handle(ListGuardiansQuery request, CancellationToken ct)
    {
        if (!await GuardianAccess.CanSeeStudentAsync(db, scope, request.StudentId, ct))
        {
            return Error.NotFound("Student not found.");
        }

        var links = await db.StudentGuardians.AsNoTracking().Include(l => l.Guardian)
            .Where(l => l.StudentId == request.StudentId && !l.Guardian!.IsDeleted)
            .OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Guardian!.FirstName)
            .ToListAsync(ct);
        return Result<IReadOnlyList<GuardianDto>>.Success(links.Select(GuardianAccess.ToDto).ToList());
    }
}

public sealed class AddGuardianHandler(IAppDbContext db) : IRequestHandler<AddGuardianCommand, Result<GuardianDto>>
{
    public async Task<Result<GuardianDto>> Handle(AddGuardianCommand request, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId, ct))
        {
            return Error.NotFound("Student not found.");
        }

        var f = request.Guardian;
        var email = f.Email.Trim().ToLowerInvariant();

        // A guardian with several children (siblings) is one record linked to each child.
        var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Email == email, ct);
        if (guardian is null)
        {
            guardian = new Guardian { Email = email };
            db.Guardians.Add(guardian);
        }
        else if (await db.StudentGuardians.AnyAsync(l => l.StudentId == request.StudentId && l.GuardianId == guardian.Id, ct))
        {
            return Error.Conflict("This guardian is already linked to the student.", "email");
        }

        guardian.FirstName = f.FirstName.Trim();
        guardian.LastName = f.LastName.Trim();
        guardian.Phone = f.Phone;
        guardian.Relationship = f.Relationship;
        guardian.IsPrimaryContact = f.IsPrimary;
        guardian.CanPickup = f.CanPickup;
        guardian.ReceivesCommunications = f.ReceivesCommunications;

        var link = new StudentGuardian { StudentId = request.StudentId, Guardian = guardian, IsPrimary = f.IsPrimary };
        db.StudentGuardians.Add(link);
        await db.SaveChangesAsync(ct);
        return Result<GuardianDto>.Success(GuardianAccess.ToDto(link));
    }
}

public sealed class UpdateGuardianHandler(IAppDbContext db) : IRequestHandler<UpdateGuardianCommand, Result<GuardianDto>>
{
    public async Task<Result<GuardianDto>> Handle(UpdateGuardianCommand request, CancellationToken ct)
    {
        var link = await db.StudentGuardians.Include(l => l.Guardian)
            .FirstOrDefaultAsync(l => l.StudentId == request.StudentId && l.GuardianId == request.GuardianId, ct);
        if (link?.Guardian is null)
        {
            return Error.NotFound("Guardian not found.");
        }

        var f = request.Guardian;
        var email = f.Email.Trim().ToLowerInvariant();
        if (await db.Guardians.AnyAsync(g => g.Id != link.GuardianId && g.Email == email, ct))
        {
            return Error.Conflict("Another guardian already uses this email.", "email");
        }

        var guardian = link.Guardian;
        guardian.FirstName = f.FirstName.Trim();
        guardian.LastName = f.LastName.Trim();
        guardian.Email = email;
        guardian.Phone = f.Phone;
        guardian.Relationship = f.Relationship;
        guardian.IsPrimaryContact = f.IsPrimary;
        guardian.CanPickup = f.CanPickup;
        guardian.ReceivesCommunications = f.ReceivesCommunications;
        link.IsPrimary = f.IsPrimary;
        await db.SaveChangesAsync(ct);
        return Result<GuardianDto>.Success(GuardianAccess.ToDto(link));
    }
}

public sealed class RemoveGuardianHandler(IAppDbContext db) : IRequestHandler<RemoveGuardianCommand, Result>
{
    public async Task<Result> Handle(RemoveGuardianCommand request, CancellationToken ct)
    {
        var link = await db.StudentGuardians.Include(l => l.Guardian)
            .FirstOrDefaultAsync(l => l.StudentId == request.StudentId && l.GuardianId == request.GuardianId, ct);
        if (link?.Guardian is null)
        {
            return Result.Failure(Error.NotFound("Guardian not found."));
        }

        db.StudentGuardians.Remove(link);

        // A guardian with no remaining children is retired (soft-deleted), keeping their history in the audit log.
        var stillLinked = await db.StudentGuardians.AnyAsync(
            l => l.GuardianId == request.GuardianId && l.StudentId != request.StudentId, ct);
        if (!stillLinked)
        {
            db.Guardians.Remove(link.Guardian);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
