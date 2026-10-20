using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.Settings;

public sealed record CentreSettingsDto(
    Dictionary<string, string> Name, string? ContactEmail, string? ContactPhone, string? KhdaLicenseNumber, IReadOnlyList<string> WeekendDays, string Timezone,
    string? Address, string? Website, string? BrandColor, Guid? LogoMediaId, int LateGraceMinutes, int EarlyLeaveGraceMinutes, bool ParentsSeeInvoices);

public sealed record GetCentreSettingsQuery : IRequest<Result<CentreSettingsDto>>;

public sealed record SaveCentreSettingsCommand(
    Dictionary<string, string> Name, string? ContactEmail, string? ContactPhone, string? KhdaLicenseNumber, IReadOnlyList<string> WeekendDays,
    string? Address, string? Website, string? BrandColor, Guid? LogoMediaId, int LateGraceMinutes, int EarlyLeaveGraceMinutes, bool ParentsSeeInvoices)
    : IRequest<Result<CentreSettingsDto>>;

public static class WeekDays
{
    public static readonly string[] All = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
}

/// <summary>What the centre has chosen, or the system defaults where it has not chosen anything.</summary>
public sealed class CentreSettingsReader(IAppDbContext db, IOptions<AttendanceOptions> attendance)
{
    public async Task<(int LateGrace, int EarlyGrace)> AttendanceAsync(CancellationToken ct)
    {
        var row = await db.CentreSettings.AsNoTracking().Select(s => new { s.LateGraceMinutes, s.EarlyLeaveGraceMinutes }).FirstOrDefaultAsync(ct);
        return (row?.LateGraceMinutes ?? attendance.Value.LateGraceMinutes, row?.EarlyLeaveGraceMinutes ?? attendance.Value.EarlyLeaveGraceMinutes);
    }

    public async Task<bool> ParentsSeeInvoicesAsync(CancellationToken ct) =>
        await db.CentreSettings.AsNoTracking().Select(s => (bool?)s.ParentsSeeInvoices).FirstOrDefaultAsync(ct) ?? true;

    public async Task<CentreSettingsDto> GetAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        var row = await db.CentreSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var (late, early) = await AttendanceAsync(ct);
        return new CentreSettingsDto(
            tenant.Name, tenant.ContactEmail, tenant.ContactPhone, tenant.KhdaLicenseNumber, tenant.WeekendDays, tenant.Timezone,
            row?.Address, row?.Website, row?.BrandColor, row?.LogoMediaId, late, early, row?.ParentsSeeInvoices ?? true);
    }
}

public sealed partial class SaveCentreSettingsValidator : AbstractValidator<SaveCentreSettingsCommand>
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    public SaveCentreSettingsValidator()
    {
        RuleFor(x => x.Name).Must(n => n is not null && n.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)).WithMessage("The centre needs an English name.");
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContactPhone).MaximumLength(40);
        RuleFor(x => x.KhdaLicenseNumber).MaximumLength(60);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Website).MaximumLength(200);
        RuleFor(x => x.BrandColor).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use a colour like #0E7C86.");
        RuleFor(x => x.WeekendDays).Must(d => d is not null && d.Count <= 6 && d.All(WeekDays.All.Contains) && d.Distinct().Count() == d.Count).WithMessage("Choose weekend days from Monday to Sunday (at most six).");
        RuleFor(x => x.LateGraceMinutes).InclusiveBetween(0, 120);
        RuleFor(x => x.EarlyLeaveGraceMinutes).InclusiveBetween(0, 120);
    }
}

public sealed class GetCentreSettingsHandler(CentreSettingsReader reader, ITenantContext tenant) : IRequestHandler<GetCentreSettingsQuery, Result<CentreSettingsDto>>
{
    public async Task<Result<CentreSettingsDto>> Handle(GetCentreSettingsQuery request, CancellationToken ct) =>
        Result<CentreSettingsDto>.Success(await reader.GetAsync(tenant.TenantId, ct));
}

public sealed class SaveCentreSettingsHandler(IAppDbContext db, CentreSettingsReader reader, ITenantContext tenant) : IRequestHandler<SaveCentreSettingsCommand, Result<CentreSettingsDto>>
{
    public async Task<Result<CentreSettingsDto>> Handle(SaveCentreSettingsCommand request, CancellationToken ct)
    {
        if (request.LogoMediaId is { } media && !await db.MediaFiles.AnyAsync(m => m.Id == media, ct))
        {
            return Error.Validation("The logo file does not exist.", "logo_media_id");
        }

        var centre = await db.Tenants.FirstAsync(t => t.Id == tenant.TenantId, ct);
        centre.Name = request.Name.Where(n => !string.IsNullOrWhiteSpace(n.Value)).ToDictionary(n => n.Key, n => n.Value.Trim());
        centre.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        centre.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();
        centre.KhdaLicenseNumber = string.IsNullOrWhiteSpace(request.KhdaLicenseNumber) ? null : request.KhdaLicenseNumber.Trim();
        centre.WeekendDays = request.WeekendDays.ToArray();

        var row = await db.CentreSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new CentreSettings();
            db.CentreSettings.Add(row);
        }

        row.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        row.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        row.BrandColor = string.IsNullOrWhiteSpace(request.BrandColor) ? null : request.BrandColor.Trim();
        row.LogoMediaId = request.LogoMediaId;
        row.LateGraceMinutes = request.LateGraceMinutes;
        row.EarlyLeaveGraceMinutes = request.EarlyLeaveGraceMinutes;
        row.ParentsSeeInvoices = request.ParentsSeeInvoices;
        await db.SaveChangesAsync(ct);
        return Result<CentreSettingsDto>.Success(await reader.GetAsync(tenant.TenantId, ct));
    }
}
