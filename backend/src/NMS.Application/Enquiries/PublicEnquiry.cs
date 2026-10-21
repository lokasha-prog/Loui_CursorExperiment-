using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Admissions;
using NMS.Shared.Results;

namespace NMS.Application.Enquiries;

/// <summary>Checks the "I am not a robot" answer from the public form. Not set up (no secret key) means every answer passes, for local development only.</summary>
public interface ICaptchaVerifier
{
    bool IsConfigured { get; }

    Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken cancellationToken);
}

public sealed record PublicEnquiryCommand(
    string Centre, string FamilyName, string ChildName, string? Phone, string? Email, string? PreferredContact, DateOnly? ChildDateOfBirth,
    DateOnly? DesiredStartDate, string? Message, string? Website, string? CaptchaToken, string? IpAddress) : IRequest<Result>;

public sealed class PublicEnquiryValidator : AbstractValidator<PublicEnquiryCommand>
{
    public PublicEnquiryValidator()
    {
        RuleFor(x => x.Centre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FamilyName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ChildName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PreferredContact).Must(c => c is null || ContactMethods.All.Contains(c)).WithMessage("Choose phone, WhatsApp or e-mail.");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Phone) || !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Give a phone number or an e-mail so we can reach you.");
        RuleFor(x => x.Message).MaximumLength(1500);
    }
}

/// <summary>
/// The public enquiry form (spec 03 §10). No sign-in, so it is guarded three ways: rate limit per address, a hidden field only robots fill in,
/// and the captcha check. A robot gets the same "thank you" so it learns nothing.
/// </summary>
public sealed class PublicEnquiryHandler(IAppDbContext db, ITenantContext tenant, ICaptchaVerifier captcha, CentreCalendar calendar) : IRequestHandler<PublicEnquiryCommand, Result>
{
    public async Task<Result> Handle(PublicEnquiryCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Result.Success();
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, request.IpAddress, ct))
        {
            return Result.Failure(Error.Validation("Please confirm you are not a robot and try again.", "captcha_token"));
        }

        var slug = request.Centre.Trim().ToLowerInvariant();
        var centre = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => !t.IsDeleted && t.Status == "active" && t.Slug == slug, ct);
        if (centre is null)
        {
            return Result.Failure(Error.NotFound("This enquiry form is not available."));
        }

        tenant.TenantId = centre.Id;
        var enquiry = new Enquiry
        {
            FamilyName = request.FamilyName.Trim(), ChildName = request.ChildName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PreferredContact = request.PreferredContact ?? "phone", ChildDateOfBirth = request.ChildDateOfBirth, DesiredStartDate = request.DesiredStartDate,
            Source = "website", Notes = request.Message?.Trim(), EnquiryDate = calendar.Today,
        };
        db.Enquiries.Add(enquiry);
        db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = enquiry.Id, ToStage = EnquiryStages.NoStage, Reason = "Online enquiry form" });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record PublicProgrammeDto(Dictionary<string, string> Name, Dictionary<string, string>? Description, int DaysPerWeek, string StartTime, string EndTime, int MinAgeMonths, int MaxAgeMonths);

public sealed record PublicCentreDto(Dictionary<string, string> Name, IReadOnlyList<PublicProgrammeDto> Programmes, int? ChildrenPerAdultAtMost, int Rooms);

/// <summary>What a family may see on the nursery's website: the centre name and its open programmes. No fees, no people, no private data.</summary>
public sealed record PublicCentreQuery(string Centre) : IRequest<Result<PublicCentreDto>>;

public sealed class PublicCentreHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<PublicCentreQuery, Result<PublicCentreDto>>
{
    public async Task<Result<PublicCentreDto>> Handle(PublicCentreQuery request, CancellationToken ct)
    {
        var slug = request.Centre.Trim().ToLowerInvariant();
        var centre = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => !t.IsDeleted && t.Status == "active" && t.Slug == slug, ct);
        if (centre is null)
        {
            return Error.NotFound("This centre is not available.");
        }

        tenant.TenantId = centre.Id;
        var programmes = await db.Programs.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.MinAgeMonths).ThenBy(p => p.MaxAgeMonths)
            .Select(p => new { p.Name, p.Description, p.DaysPerWeek, p.StartTime, p.EndTime, p.MinAgeMonths, p.MaxAgeMonths }).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive).Select(r => r.RatioStaffToStudent).ToListAsync(ct);
        var ratios = rooms.Where(r => r > 0).ToList();
        return Result<PublicCentreDto>.Success(new PublicCentreDto(
            centre.Name,
            programmes.Select(p => new PublicProgrammeDto(p.Name, p.Description, p.DaysPerWeek, p.StartTime.ToString("HH:mm"), p.EndTime.ToString("HH:mm"), p.MinAgeMonths, p.MaxAgeMonths)).ToList(),
            ratios.Count > 0 ? ratios.Max() : null, rooms.Count));
    }
}
