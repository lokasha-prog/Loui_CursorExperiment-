using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Domain.CentreManagement;
using NMS.Shared.Results;

namespace NMS.Application.Tools;

/// <summary>One ready-made card design. The colours are only starting points; the centre can change them.</summary>
public sealed record IdCardTemplate(string Id, string Scope, Dictionary<string, string> Name, string Layout, string Primary, string Secondary, string Accent);

public sealed record IdCardRequest(
    string Scope, IReadOnlyList<Guid> Ids, string Template, string? Primary, string? Secondary, string? Accent, IReadOnlyList<string>? Fields,
    DateOnly? Expiry, string? Size, string? Language);

public sealed record IdCardPerson(string Name, string? Subtitle, byte[]? Photo, DateOnly? DateOfBirth, string? Room, string? Program, string Barcode);

public sealed record IdCardStyle(
    string Layout, string Primary, string Secondary, string Accent, IReadOnlySet<string> Fields, DateOnly? Expiry, string Size, string Language, string CentreName, byte[]? Logo, string Scope);

public sealed record IdCardPdf(byte[] Pdf, string FileName);

public sealed record GetIdCardTemplatesQuery(string? Scope) : IRequest<Result<IReadOnlyList<IdCardTemplate>>>;

public sealed record GenerateIdCardsQuery(IdCardRequest Request) : IRequest<Result<IdCardPdf>>;

/// <summary>Draws the cards. Implemented outside this layer so no PDF library leaks in here.</summary>
public interface IIdCardRenderer
{
    byte[] Render(IReadOnlyList<IdCardPerson> people, IdCardStyle style);
}

public static class IdCardCatalogue
{
    public static readonly string[] FieldNames = ["name", "photo", "dob", "room", "program", "barcode", "expiry"];

    public static readonly IReadOnlyList<IdCardTemplate> All =
    [
        new("student-sunshine", "student", Names("Sunshine", "شروق"), "band", "#F59E0B", "#FEF3C7", "#B45309"),
        new("student-ocean", "student", Names("Ocean", "المحيط"), "pill", "#0E7490", "#CFFAFE", "#0891B2"),
        new("student-forest", "student", Names("Forest", "الغابة"), "side", "#15803D", "#DCFCE7", "#166534"),
        new("student-berry", "student", Names("Berry", "التوت"), "frame", "#BE185D", "#FCE7F3", "#9D174D"),
        new("student-rainbow", "student", Names("Rainbow", "قوس قزح"), "band", "#7C3AED", "#EDE9FE", "#F97316"),
        new("staff-professional", "staff", Names("Professional", "احترافي"), "band", "#0F172A", "#E2E8F0", "#0E7C86"),
        new("staff-modern", "staff", Names("Modern", "عصري"), "side", "#0E7C86", "#CCFBF1", "#0F766E"),
        new("staff-bold", "staff", Names("Bold", "جريء"), "pill", "#B91C1C", "#FEE2E2", "#7F1D1D"),
        new("staff-clean", "staff", Names("Clean", "نظيف"), "minimal", "#1D4ED8", "#DBEAFE", "#1E40AF"),
        new("staff-stripe", "staff", Names("Stripe", "خطوط"), "frame", "#334155", "#F1F5F9", "#F59E0B"),
    ];

    private static Dictionary<string, string> Names(string en, string ar) => new() { ["en"] = en, ["ar"] = ar };
}

public sealed partial class GenerateIdCardsValidator : AbstractValidator<GenerateIdCardsQuery>
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    public GenerateIdCardsValidator()
    {
        RuleFor(x => x.Request.Scope).Must(s => s is "student" or "staff").WithMessage("Choose students or staff.");
        RuleFor(x => x.Request.Ids).Must(i => i is { Count: > 0 and <= 200 }).WithMessage("Choose between 1 and 200 people.");
        RuleFor(x => x.Request.Template).Must(t => IdCardCatalogue.All.Any(c => c.Id == t)).WithMessage("Unknown template.");
        RuleFor(x => x.Request).Must(r => IdCardCatalogue.All.First(c => c.Id == r.Template).Scope == r.Scope).When(x => IdCardCatalogue.All.Any(c => c.Id == x.Request.Template)).WithMessage("That template is for the other group.");
        RuleFor(x => x.Request.Primary).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use colours like #0E7C86.");
        RuleFor(x => x.Request.Secondary).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use colours like #0E7C86.");
        RuleFor(x => x.Request.Accent).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use colours like #0E7C86.");
        RuleFor(x => x.Request.Fields).Must(f => f is null || f.All(IdCardCatalogue.FieldNames.Contains)).WithMessage("Unknown field.");
        RuleFor(x => x.Request.Size).Must(s => s is null or "a6" or "cr80").WithMessage("Size must be a6 or cr80.");
        RuleFor(x => x.Request.Language).Must(l => l is null or "en" or "ar").WithMessage("Language must be en or ar.");
    }
}

public sealed class GetIdCardTemplatesHandler : IRequestHandler<GetIdCardTemplatesQuery, Result<IReadOnlyList<IdCardTemplate>>>
{
    public Task<Result<IReadOnlyList<IdCardTemplate>>> Handle(GetIdCardTemplatesQuery request, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<IdCardTemplate>>.Success(IdCardCatalogue.All.Where(t => request.Scope is null || t.Scope == request.Scope).ToList()));
}

/// <summary>Builds the print-ready PDF of ID cards, one card per page, with a QR code staff can scan for attendance.</summary>
public sealed class GenerateIdCardsHandler(IAppDbContext db, ITenantContext tenant, IIdCardRenderer renderer) : IRequestHandler<GenerateIdCardsQuery, Result<IdCardPdf>>
{
    public async Task<Result<IdCardPdf>> Handle(GenerateIdCardsQuery query, CancellationToken ct)
    {
        var r = query.Request;
        var template = IdCardCatalogue.All.First(t => t.Id == r.Template);
        var language = r.Language ?? "en";
        var ids = r.Ids.Distinct().ToList();
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        string? Pick(Dictionary<string, string>? text) => text is null ? null : text.GetValueOrDefault(language) ?? text.GetValueOrDefault("en") ?? text.Values.FirstOrDefault();

        var people = new List<IdCardPerson>();
        if (r.Scope == "student")
        {
            var kids = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ToListAsync(ct);
            var photoIds = kids.Where(k => k.PhotoMediaId != null).Select(k => k.PhotoMediaId!.Value).ToList();
            var photos = await db.MediaFiles.AsNoTracking().Where(m => photoIds.Contains(m.Id) && m.ContentType.StartsWith("image/")).ToDictionaryAsync(m => m.Id, m => m.Content, ct);
            foreach (var k in kids)
            {
                var name = language == "ar" && !string.IsNullOrWhiteSpace(k.FirstNameArabic) ? $"{k.FirstNameArabic} {k.LastNameArabic}".Trim() : $"{k.FirstName} {k.LastName}";
                people.Add(new IdCardPerson(
                    name, null, k.PhotoMediaId is { } p ? photos.GetValueOrDefault(p) : null, k.DateOfBirth,
                    k.RoomId is { } room ? Pick(rooms.GetValueOrDefault(room)) : null, k.ProgramId is { } pr ? Pick(programs.GetValueOrDefault(pr)) : null, k.Barcode));
            }
        }
        else
        {
            var staff = await db.Staff.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ToListAsync(ct);
            people.AddRange(staff.Select(s => new IdCardPerson($"{s.FirstName} {s.LastName}", s.JobTitle, null, s.DateOfBirth, null, null, s.Barcode)));
        }

        if (people.Count == 0)
        {
            return Error.NotFound("None of the chosen people were found.");
        }

        var centre = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Name).FirstAsync(ct);
        var settings = await db.CentreSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        byte[]? logo = settings?.LogoMediaId is { } logoId
            ? await db.MediaFiles.AsNoTracking().Where(m => m.Id == logoId && m.ContentType.StartsWith("image/")).Select(m => m.Content).FirstOrDefaultAsync(ct)
            : null;
        var fields = (r.Fields ?? IdCardCatalogue.FieldNames).ToHashSet();
        var style = new IdCardStyle(
            template.Layout, r.Primary ?? settings?.BrandColor ?? template.Primary, r.Secondary ?? template.Secondary, r.Accent ?? template.Accent,
            fields, r.Expiry, r.Size ?? "a6", language, Pick(centre) ?? string.Empty, logo, r.Scope);
        return Result<IdCardPdf>.Success(new IdCardPdf(renderer.Render(people, style), $"id-cards-{r.Scope}-{DateTime.UtcNow:yyyyMMdd}.pdf"));
    }
}
