using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Students;
using NMS.Domain.Admissions;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Enquiries;

public sealed record EnquiryFields(
    string FamilyName, string ChildName, string? Phone, string? Email, string? PreferredContact, DateOnly? ChildDateOfBirth,
    DateOnly? DesiredStartDate, Guid? ProgramId, string Source, Guid? AssignedStaffId, string? Notes);

public sealed record EnquiryDto(
    Guid Id, string FamilyName, string ChildName, string? Phone, string? Email, string PreferredContact, DateOnly? ChildDateOfBirth,
    DateOnly? DesiredStartDate, Guid? ProgramId, string Source, Guid? AssignedStaffId, string? AssignedStaffName, string Stage,
    DateOnly EnquiryDate, DateOnly? LastContactDate, string? Notes, bool IsArchived, Guid? StudentId, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record StageLogDto(string? FromStage, string ToStage, string? Reason, DateTimeOffset At);

public sealed record ActivityDto(Guid Id, string Kind, string Summary, DateTimeOffset At);

public sealed record EnquiryDetailDto(EnquiryDto Enquiry, IReadOnlyList<StageLogDto> History, IReadOnlyList<ActivityDto> Activities);

public sealed record ListEnquiriesQuery(string? Stage, string? Source, Guid? StaffId, DateOnly? From, DateOnly? To, bool Archived, string? Search, string? Cursor, int? Limit)
    : IRequest<Result<CursorPage<EnquiryDto>>>;

public sealed record GetEnquiryQuery(Guid Id) : IRequest<Result<EnquiryDetailDto>>;

public sealed record SaveEnquiryCommand(Guid? Id, EnquiryFields Fields) : IRequest<Result<EnquiryDetailDto>>;

public sealed record ChangeStageCommand(Guid Id, string Stage, string? Reason) : IRequest<Result<EnquiryDetailDto>>;

public sealed record AddActivityCommand(Guid Id, string Kind, string Summary) : IRequest<Result<EnquiryDetailDto>>;

public sealed record ArchiveEnquiryCommand(Guid Id, bool Archived) : IRequest<Result<EnquiryDetailDto>>;

public sealed record ConvertEnquiryCommand(Guid Id, Guid? RoomId, DateOnly? StartDate) : IRequest<Result<EnquiryDetailDto>>;

public sealed record StageCount(string Stage, int Count);

public sealed record SourceCount(string Source, int Total, int Enrolled);

public sealed record EnquirySummaryDto(int Total, IReadOnlyList<StageCount> ByStage, IReadOnlyList<SourceCount> BySource, int NewThisMonth, int Enrolled);

public sealed record EnquirySummaryQuery : IRequest<Result<EnquirySummaryDto>>;

public sealed class SaveEnquiryValidator : AbstractValidator<SaveEnquiryCommand>
{
    public SaveEnquiryValidator()
    {
        RuleFor(x => x.Fields.FamilyName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Fields.ChildName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Fields.Source).Must(s => EnquirySources.All.Contains(s)).WithMessage("Choose one of the listed sources.");
        RuleFor(x => x.Fields.PreferredContact).Must(c => c is null || ContactMethods.All.Contains(c)).WithMessage("Choose phone, WhatsApp or e-mail.");
        RuleFor(x => x.Fields.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Fields.Email));
        RuleFor(x => x.Fields.Phone).MaximumLength(40);
        RuleFor(x => x.Fields).Must(f => !string.IsNullOrWhiteSpace(f.Phone) || !string.IsNullOrWhiteSpace(f.Email)).WithMessage("Give a phone number or an e-mail so the family can be reached.");
        RuleFor(x => x.Fields.Notes).MaximumLength(2000);
    }
}

public sealed class ChangeStageValidator : AbstractValidator<ChangeStageCommand>
{
    public ChangeStageValidator()
    {
        RuleFor(x => x.Stage).Must(s => EnquiryStages.All.Contains(s)).WithMessage("Unknown stage.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class AddActivityValidator : AbstractValidator<AddActivityCommand>
{
    public AddActivityValidator()
    {
        RuleFor(x => x.Kind).Must(k => ContactKinds.All.Contains(k)).WithMessage("Unknown contact type.");
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(1000);
    }
}

internal static class EnquiryMapping
{
    public static async Task<EnquiryDetailDto> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var e = await db.Enquiries.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var staff = e.AssignedStaffId is { } sid ? await db.Staff.AsNoTracking().Where(s => s.Id == sid).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) : null;
        var history = await db.EnquiryStageLogs.AsNoTracking().Where(l => l.EnquiryId == id).OrderBy(l => l.CreatedAt).Select(l => new StageLogDto(l.FromStage, l.ToStage, l.Reason, l.CreatedAt)).ToListAsync(ct);
        var activities = await db.EnquiryActivities.AsNoTracking().Where(a => a.EnquiryId == id).OrderByDescending(a => a.CreatedAt).Select(a => new ActivityDto(a.Id, a.Kind, a.Summary, a.CreatedAt)).ToListAsync(ct);
        return new EnquiryDetailDto(ToDto(e, staff), history, activities);
    }

    public static EnquiryDto ToDto(Enquiry e, string? staffName) => new(
        e.Id, e.FamilyName, e.ChildName, e.Phone, e.Email, e.PreferredContact, e.ChildDateOfBirth, e.DesiredStartDate, e.ProgramId, e.Source,
        e.AssignedStaffId, staffName, e.Stage, e.EnquiryDate, e.LastContactDate, e.Notes, e.IsArchived, e.StudentId, e.CreatedAt);

    public static async Task<Error?> CheckLinksAsync(IAppDbContext db, EnquiryFields f, CancellationToken ct)
    {
        if (f.ProgramId is { } p && !await db.Programs.AnyAsync(x => x.Id == p, ct))
        {
            return Error.Validation("The programme does not exist.", "program_id");
        }

        if (f.AssignedStaffId is { } s && !await db.Staff.AnyAsync(x => x.Id == s, ct))
        {
            return Error.Validation("The staff member does not exist.", "assigned_staff_id");
        }

        return null;
    }
}

public sealed class ListEnquiriesHandler(IAppDbContext db) : IRequestHandler<ListEnquiriesQuery, Result<CursorPage<EnquiryDto>>>
{
    public async Task<Result<CursorPage<EnquiryDto>>> Handle(ListEnquiriesQuery request, CancellationToken ct)
    {
        var q = db.Enquiries.AsNoTracking().Where(e => e.IsArchived == request.Archived);
        if (request.Stage is not null) q = q.Where(e => e.Stage == request.Stage);
        if (request.Source is not null) q = q.Where(e => e.Source == request.Source);
        if (request.StaffId is { } staff) q = q.Where(e => e.AssignedStaffId == staff);
        if (request.From is { } from) q = q.Where(e => e.EnquiryDate >= from);
        if (request.To is { } to) q = q.Where(e => e.EnquiryDate <= to);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(e => e.FamilyName.Contains(term) || e.ChildName.Contains(term) || (e.Phone != null && e.Phone.Contains(term)) || (e.Email != null && e.Email.Contains(term)));
        }

        return Result<CursorPage<EnquiryDto>>.Success(await q.ToCursorPageAsync(request.Cursor, request.Limit,
            e => new EnquiryDto(
                e.Id, e.FamilyName, e.ChildName, e.Phone, e.Email, e.PreferredContact, e.ChildDateOfBirth, e.DesiredStartDate, e.ProgramId, e.Source, e.AssignedStaffId,
                db.Staff.Where(s => s.Id == e.AssignedStaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
                e.Stage, e.EnquiryDate, e.LastContactDate, e.Notes, e.IsArchived, e.StudentId, e.CreatedAt), ct));
    }
}

public sealed class GetEnquiryHandler(IAppDbContext db) : IRequestHandler<GetEnquiryQuery, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(GetEnquiryQuery request, CancellationToken ct) =>
        await db.Enquiries.AnyAsync(e => e.Id == request.Id, ct) ? Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, request.Id, ct)) : Error.NotFound("Enquiry not found.");
}

public sealed class SaveEnquiryHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<SaveEnquiryCommand, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(SaveEnquiryCommand request, CancellationToken ct)
    {
        var f = request.Fields;
        if (await EnquiryMapping.CheckLinksAsync(db, f, ct) is { } bad)
        {
            return bad;
        }

        Enquiry e;
        if (request.Id is { } id)
        {
            var found = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Enquiry not found.");
            }

            e = found;
        }
        else
        {
            e = new Enquiry { EnquiryDate = calendar.Today };
            db.Enquiries.Add(e);
            db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = e.Id, FromStage = null, ToStage = EnquiryStages.NoStage, Reason = "Enquiry received" });
        }

        e.FamilyName = f.FamilyName.Trim();
        e.ChildName = f.ChildName.Trim();
        e.Phone = string.IsNullOrWhiteSpace(f.Phone) ? null : f.Phone.Trim();
        e.Email = string.IsNullOrWhiteSpace(f.Email) ? null : f.Email.Trim();
        e.PreferredContact = f.PreferredContact ?? "phone";
        e.ChildDateOfBirth = f.ChildDateOfBirth;
        e.DesiredStartDate = f.DesiredStartDate;
        e.ProgramId = f.ProgramId;
        e.Source = f.Source;
        e.AssignedStaffId = f.AssignedStaffId;
        e.Notes = f.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, e.Id, ct));
    }
}

public sealed class ChangeStageHandler(IAppDbContext db) : IRequestHandler<ChangeStageCommand, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(ChangeStageCommand request, CancellationToken ct)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null)
        {
            return Error.NotFound("Enquiry not found.");
        }

        if (request.Stage == EnquiryStages.Enrolled)
        {
            return Error.InvalidState("A child becomes enrolled by converting the enquiry to a student.");
        }

        if (e.Stage == EnquiryStages.Enrolled)
        {
            return Error.InvalidState("This enquiry is already enrolled.");
        }

        if (e.Stage != request.Stage)
        {
            db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = e.Id, FromStage = e.Stage, ToStage = request.Stage, Reason = request.Reason?.Trim() });
            e.Stage = request.Stage;
            await db.SaveChangesAsync(ct);
        }

        return Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, e.Id, ct));
    }
}

public sealed class AddActivityHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<AddActivityCommand, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(AddActivityCommand request, CancellationToken ct)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null)
        {
            return Error.NotFound("Enquiry not found.");
        }

        db.EnquiryActivities.Add(new EnquiryActivity { EnquiryId = e.Id, Kind = request.Kind, Summary = request.Summary.Trim() });
        if (request.Kind != "note")
        {
            e.LastContactDate = calendar.Today;
            if (e.Stage == EnquiryStages.NoStage || e.Stage == EnquiryStages.Interested)
            {
                db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = e.Id, FromStage = e.Stage, ToStage = EnquiryStages.Contacted, Reason = "Contact made" });
                e.Stage = EnquiryStages.Contacted;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, e.Id, ct));
    }
}

public sealed class ArchiveEnquiryHandler(IAppDbContext db) : IRequestHandler<ArchiveEnquiryCommand, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(ArchiveEnquiryCommand request, CancellationToken ct)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null)
        {
            return Error.NotFound("Enquiry not found.");
        }

        e.IsArchived = request.Archived;
        await db.SaveChangesAsync(ct);
        return Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, e.Id, ct));
    }
}

/// <summary>Creates the child (status "signed up") and the first parent from the enquiry, then marks it enrolled and keeps the link for analytics.</summary>
public sealed class ConvertEnquiryHandler(IAppDbContext db, ISender sender) : IRequestHandler<ConvertEnquiryCommand, Result<EnquiryDetailDto>>
{
    public async Task<Result<EnquiryDetailDto>> Handle(ConvertEnquiryCommand request, CancellationToken ct)
    {
        var e = await db.Enquiries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null)
        {
            return Error.NotFound("Enquiry not found.");
        }

        if (e.StudentId is not null)
        {
            return Error.InvalidState("This enquiry has already been converted.");
        }

        if (e.ChildDateOfBirth is not { } dob)
        {
            return Error.Validation("Add the child's date of birth before enrolling.", "child_date_of_birth");
        }

        if (string.IsNullOrWhiteSpace(e.Email))
        {
            return Error.Validation("Add the parent's e-mail before enrolling, so they can be invited.", "email");
        }

        var parts = e.ChildName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = parts[0];
        var last = parts.Length > 1 ? parts[1] : e.FamilyName;
        var fields = new StudentFields(
            first, last, null, null, dob, null, request.RoomId, null, e.ProgramId, null, request.StartDate ?? e.DesiredStartDate,
            null, null, null, null, null, null, null, null, null, null);
        var guardian = new NewGuardianInput(e.FamilyName, e.FamilyName, e.Email!, e.Phone, "other", true);

        var created = await sender.Send(new CreateStudentCommand(fields, [guardian]), ct);
        if (created.IsFailure)
        {
            return Result<EnquiryDetailDto>.Failure(created.Errors);
        }

        db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = e.Id, FromStage = e.Stage, ToStage = EnquiryStages.Enrolled, Reason = "Converted to student" });
        e.Stage = EnquiryStages.Enrolled;
        e.StudentId = created.Value.Id;
        await db.SaveChangesAsync(ct);
        return Result<EnquiryDetailDto>.Success(await EnquiryMapping.DetailAsync(db, e.Id, ct));
    }
}

/// <summary>Where enquiries are in the pipeline and which sources bring families who actually enrol.</summary>
public sealed class EnquirySummaryHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<EnquirySummaryQuery, Result<EnquirySummaryDto>>
{
    public async Task<Result<EnquirySummaryDto>> Handle(EnquirySummaryQuery request, CancellationToken ct)
    {
        var live = db.Enquiries.AsNoTracking().Where(e => !e.IsArchived);
        var stages = await live.GroupBy(e => e.Stage).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var sources = await live.GroupBy(e => e.Source).Select(g => new { g.Key, Total = g.Count(), Enrolled = g.Count(x => x.Stage == EnquiryStages.Enrolled) }).ToListAsync(ct);
        var monthStart = new DateOnly(calendar.Today.Year, calendar.Today.Month, 1);
        var recent = await live.CountAsync(e => e.EnquiryDate >= monthStart, ct);
        return Result<EnquirySummaryDto>.Success(new EnquirySummaryDto(
            stages.Sum(s => s.N),
            EnquiryStages.All.Select(s => new StageCount(s, stages.FirstOrDefault(x => x.Key == s)?.N ?? 0)).ToList(),
            sources.OrderByDescending(s => s.Total).Select(s => new SourceCount(s.Key, s.Total, s.Enrolled)).ToList(),
            recent, stages.FirstOrDefault(s => s.Key == EnquiryStages.Enrolled)?.N ?? 0));
    }
}
