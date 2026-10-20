using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Students;

public sealed record StudentSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? FirstNameArabic,
    string? LastNameArabic,
    DateOnly DateOfBirth,
    string? Gender,
    Guid? RoomId,
    Guid? GroupId,
    Guid? ProgramId,
    string Status,
    string Barcode,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record StudentDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    Dictionary<string, string>? DisplayName,
    string? FirstNameArabic,
    string? LastNameArabic,
    DateOnly DateOfBirth,
    string? Gender,
    Guid? RoomId,
    Guid? GroupId,
    Guid? ProgramId,
    Guid? KeyPersonStaffId,
    string Status,
    DateTimeOffset StatusChangedAt,
    string? StatusReason,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Barcode,
    string? Nationality,
    string? Religion,
    string? PrimaryLanguage,
    string? MedicalNotes,
    string? AllergyInfo,
    string? DietaryRequirements,
    string? CustodyNotes,
    string? PassportNumber,
    string? EmiratesId,
    IReadOnlyList<Guid> LabelIds,
    DateTimeOffset CreatedAt) : ICursorItem;

public sealed record NewGuardianInput(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Relationship,
    bool IsPrimary = false);

public sealed record StudentFields(
    string FirstName,
    string LastName,
    string? FirstNameArabic,
    string? LastNameArabic,
    DateOnly DateOfBirth,
    string? Gender,
    Guid? RoomId,
    Guid? GroupId,
    Guid? ProgramId,
    Guid? KeyPersonStaffId,
    DateOnly? StartDate,
    string? Nationality,
    string? Religion,
    string? PrimaryLanguage,
    string? MedicalNotes,
    string? AllergyInfo,
    string? DietaryRequirements,
    string? CustodyNotes,
    string? PassportNumber,
    string? EmiratesId,
    IReadOnlyList<Guid>? LabelIds);

public sealed record ListStudentsQuery(string? Cursor, int? Limit, string? Status, Guid? RoomId, string? Search)
    : IRequest<Result<CursorPage<StudentSummaryDto>>>;

public sealed record GetStudentQuery(Guid Id) : IRequest<Result<StudentDetailDto>>;

public sealed record CreateStudentCommand(StudentFields Student, IReadOnlyList<NewGuardianInput>? Guardians)
    : IRequest<Result<StudentDetailDto>>;

public sealed record UpdateStudentCommand(Guid Id, StudentFields Student) : IRequest<Result<StudentDetailDto>>;

public sealed record DeleteStudentCommand(Guid Id) : IRequest<Result>;

public sealed record TransitionStudentStatusCommand(Guid Id, string Status, string? Reason)
    : IRequest<Result<StudentDetailDto>>;

public static class StudentGenders
{
    public static readonly IReadOnlyList<string> All = ["male", "female", "other", "unspecified"];
}

public sealed class StudentFieldsValidator : AbstractValidator<StudentFields>
{
    public StudentFieldsValidator()
    {
        RuleFor(s => s.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(s => s.LastName).NotEmpty().MaximumLength(100);
        RuleFor(s => s.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");
        RuleFor(s => s.Gender!)
            .Must(g => StudentGenders.All.Contains(g))
            .When(s => s.Gender is not null)
            .WithMessage("Gender must be male, female, other or unspecified.");
    }
}

public sealed class NewGuardianInputValidator : AbstractValidator<NewGuardianInput>
{
    public NewGuardianInputValidator()
    {
        RuleFor(g => g.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(g => g.LastName).NotEmpty().MaximumLength(100);
        RuleFor(g => g.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(g => g.Relationship).Must(r => GuardianRelationships.All.Contains(r))
            .WithMessage("Unknown relationship.");
    }
}

public sealed class CreateStudentValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentValidator()
    {
        RuleFor(c => c.Student).NotNull().SetValidator(new StudentFieldsValidator());
        RuleForEach(c => c.Guardians!).SetValidator(new NewGuardianInputValidator()).When(c => c.Guardians is not null);
    }
}

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator() =>
        RuleFor(c => c.Student).NotNull().SetValidator(new StudentFieldsValidator());
}

public sealed class TransitionStudentStatusValidator : AbstractValidator<TransitionStudentStatusCommand>
{
    public TransitionStudentStatusValidator()
    {
        RuleFor(c => c.Status).Must(s => StudentStatuses.All.Contains(s)).WithMessage("Unknown status.");
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}

/// <summary>Scoping rules shared by every student-facing query (spec 09 §2.4).</summary>
internal static class StudentScope
{
    public static async Task<IQueryable<Student>> ScopeAsync(
        this IQueryable<Student> students, IAccessScope scope, CancellationToken ct)
    {
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

        return students;
    }

    /// <summary>Parents never see custody notes or identity documents; everyone else with read access does.</summary>
    public static StudentDetailDto ToDetail(Student s, bool isParent) => new(
        s.Id, s.FirstName, s.LastName, s.DisplayName, s.FirstNameArabic, s.LastNameArabic, s.DateOfBirth, s.Gender,
        s.RoomId, s.GroupId, s.ProgramId, s.KeyPersonStaffId, s.Status, s.StatusChangedAt, s.StatusReason,
        s.StartDate, s.EndDate, s.Barcode, s.Nationality, s.Religion, s.PrimaryLanguage,
        s.MedicalNotes, s.AllergyInfo, s.DietaryRequirements,
        isParent ? null : s.CustodyNotes,
        isParent ? null : s.PassportNumber,
        isParent ? null : s.EmiratesId,
        s.Labels.Select(l => l.LabelId).ToList(),
        s.CreatedAt);

    public static bool IsParentOnly(ICurrentUser user) =>
        user.IsInRole(SystemRoles.Parent)
        && !user.Roles.Any(r => r is SystemRoles.SuperAdmin or SystemRoles.CentreAdmin or SystemRoles.It
            or SystemRoles.Teacher or SystemRoles.Receptionist);

    public static async Task<Result> ValidateReferencesAsync(IAppDbContext db, StudentFields f, CancellationToken ct)
    {
        if (f.RoomId is { } roomId && !await db.Rooms.AnyAsync(r => r.Id == roomId, ct))
        {
            return Result.Failure(Error.Validation("The selected room does not exist.", "room_id"));
        }

        if (f.GroupId is { } groupId && !await db.StudentGroups.AnyAsync(g => g.Id == groupId, ct))
        {
            return Result.Failure(Error.Validation("The selected group does not exist.", "group_id"));
        }

        if (f.ProgramId is { } programId && !await db.Programs.AnyAsync(p => p.Id == programId, ct))
        {
            return Result.Failure(Error.Validation("The selected program does not exist.", "program_id"));
        }

        if (f.KeyPersonStaffId is { } staffId && !await db.Staff.AnyAsync(s => s.Id == staffId, ct))
        {
            return Result.Failure(Error.Validation("The selected key person does not exist.", "key_person_staff_id"));
        }

        if (f.LabelIds is { Count: > 0 })
        {
            var found = await db.Labels.CountAsync(l => f.LabelIds.Contains(l.Id), ct);
            if (found != f.LabelIds.Distinct().Count())
            {
                return Result.Failure(Error.Validation("One or more labels do not exist.", "label_ids"));
            }
        }

        return Result.Success();
    }

    public static void Apply(Student s, StudentFields f)
    {
        s.FirstName = f.FirstName.Trim();
        s.LastName = f.LastName.Trim();
        s.FirstNameArabic = f.FirstNameArabic;
        s.LastNameArabic = f.LastNameArabic;
        s.DateOfBirth = f.DateOfBirth;
        s.Gender = f.Gender;
        s.RoomId = f.RoomId;
        s.GroupId = f.GroupId;
        s.ProgramId = f.ProgramId;
        s.KeyPersonStaffId = f.KeyPersonStaffId;
        s.StartDate = f.StartDate ?? s.StartDate;
        s.Nationality = f.Nationality;
        s.Religion = f.Religion;
        s.PrimaryLanguage = f.PrimaryLanguage;
        s.MedicalNotes = f.MedicalNotes;
        s.AllergyInfo = f.AllergyInfo;
        s.DietaryRequirements = f.DietaryRequirements;
        s.CustodyNotes = f.CustodyNotes;
        s.PassportNumber = f.PassportNumber;
        s.EmiratesId = f.EmiratesId;
    }
}

public sealed class ListStudentsHandler(IAppDbContext db, IAccessScope scope)
    : IRequestHandler<ListStudentsQuery, Result<CursorPage<StudentSummaryDto>>>
{
    public async Task<Result<CursorPage<StudentSummaryDto>>> Handle(ListStudentsQuery request, CancellationToken ct)
    {
        var students = await db.Students.AsNoTracking().ScopeAsync(scope, ct);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            students = students.Where(s => s.Status == request.Status);
        }

        if (request.RoomId is { } roomId)
        {
            students = students.Where(s => s.RoomId == roomId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            students = students.Where(s =>
                s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term) || s.Barcode.ToLower().Contains(term));
        }

        var page = await students.ToCursorPageAsync(
            request.Cursor,
            request.Limit,
            s => new StudentSummaryDto(
                s.Id, s.FirstName, s.LastName, s.FirstNameArabic, s.LastNameArabic, s.DateOfBirth, s.Gender,
                s.RoomId, s.GroupId, s.ProgramId, s.Status, s.Barcode, s.CreatedAt),
            ct);
        return Result<CursorPage<StudentSummaryDto>>.Success(page);
    }
}

public sealed class GetStudentHandler(IAppDbContext db, IAccessScope scope, ICurrentUser user)
    : IRequestHandler<GetStudentQuery, Result<StudentDetailDto>>
{
    public async Task<Result<StudentDetailDto>> Handle(GetStudentQuery request, CancellationToken ct)
    {
        // Ownership/room scope is applied inside the query: a denied row is indistinguishable from a missing one (404, never 403).
        var scoped = await db.Students.AsNoTracking().Include(s => s.Labels).ScopeAsync(scope, ct);
        var student = await scoped.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        return student is null
            ? Error.NotFound("Student not found.")
            : Result<StudentDetailDto>.Success(StudentScope.ToDetail(student, StudentScope.IsParentOnly(user)));
    }
}

public sealed class CreateStudentHandler(IAppDbContext db, ITenantContext tenant, IClock clock)
    : IRequestHandler<CreateStudentCommand, Result<StudentDetailDto>>
{
    public async Task<Result<StudentDetailDto>> Handle(CreateStudentCommand request, CancellationToken ct)
    {
        var references = await StudentScope.ValidateReferencesAsync(db, request.Student, ct);
        if (references.IsFailure)
        {
            return Result<StudentDetailDto>.Failure(references.Errors);
        }

        var student = new Student
        {
            Status = StudentStatuses.SignedUp,
            StatusChangedAt = clock.UtcNow,
            Barcode = await NextBarcodeAsync(ct),
        };
        StudentScope.Apply(student, request.Student);

        foreach (var labelId in request.Student.LabelIds?.Distinct() ?? [])
        {
            student.Labels.Add(new StudentLabel { LabelId = labelId });
        }

        db.Students.Add(student);

        foreach (var input in request.Guardians ?? [])
        {
            var email = input.Email.Trim().ToLowerInvariant();
            var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Email == email, ct);
            if (guardian is null)
            {
                guardian = new Guardian
                {
                    FirstName = input.FirstName.Trim(),
                    LastName = input.LastName.Trim(),
                    Email = email,
                    Phone = input.Phone,
                    Relationship = input.Relationship,
                    IsPrimaryContact = input.IsPrimary,
                };
                db.Guardians.Add(guardian);
            }

            student.StudentGuardians.Add(new StudentGuardian { Guardian = guardian, IsPrimary = input.IsPrimary });
        }

        await db.SaveChangesAsync(ct);
        return Result<StudentDetailDto>.Success(StudentScope.ToDetail(student, isParent: false));
    }

    private async Task<string> NextBarcodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var candidate = BarcodeGenerator.Next("STU");

            // Unique per tenant including soft-deleted rows, so the tenant filter is re-applied by hand.
            var taken = await db.Students.IgnoreQueryFilters()
                .AnyAsync(s => s.TenantId == tenant.TenantId && s.Barcode == candidate, ct);
            if (!taken)
            {
                return candidate;
            }
        }
    }
}

public sealed class UpdateStudentHandler(IAppDbContext db) : IRequestHandler<UpdateStudentCommand, Result<StudentDetailDto>>
{
    public async Task<Result<StudentDetailDto>> Handle(UpdateStudentCommand request, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.Labels).FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (student is null)
        {
            return Error.NotFound("Student not found.");
        }

        var references = await StudentScope.ValidateReferencesAsync(db, request.Student, ct);
        if (references.IsFailure)
        {
            return Result<StudentDetailDto>.Failure(references.Errors);
        }

        StudentScope.Apply(student, request.Student);

        if (request.Student.LabelIds is not null)
        {
            var wanted = request.Student.LabelIds.Distinct().ToHashSet();
            foreach (var link in student.Labels.Where(l => !wanted.Contains(l.LabelId)).ToList())
            {
                student.Labels.Remove(link);
                db.StudentLabels.Remove(link);
            }

            foreach (var labelId in wanted.Where(id => student.Labels.All(l => l.LabelId != id)))
            {
                student.Labels.Add(new StudentLabel { StudentId = student.Id, LabelId = labelId });
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<StudentDetailDto>.Success(StudentScope.ToDetail(student, isParent: false));
    }
}

public sealed class DeleteStudentHandler(IAppDbContext db) : IRequestHandler<DeleteStudentCommand, Result>
{
    public async Task<Result> Handle(DeleteStudentCommand request, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (student is null)
        {
            return Result.Failure(Error.NotFound("Student not found."));
        }

        db.Students.Remove(student);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class TransitionStudentStatusHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<TransitionStudentStatusCommand, Result<StudentDetailDto>>
{
    public async Task<Result<StudentDetailDto>> Handle(TransitionStudentStatusCommand request, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.Labels).FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (student is null)
        {
            return Error.NotFound("Student not found.");
        }

        var result = student.TransitionTo(request.Status, request.Reason, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result<StudentDetailDto>.Failure(result.Errors);
        }

        await db.SaveChangesAsync(ct);
        return Result<StudentDetailDto>.Success(StudentScope.ToDetail(student, isParent: false));
    }
}
