using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Domain.CentreManagement;
using NMS.Domain.Forms;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Tools;

public sealed record CertificateTemplate(string Id, string Code, Dictionary<string, string> Name, string Layout, string Primary, string Accent, Dictionary<string, string> DefaultTitle, Dictionary<string, string> DefaultBody);

public sealed record Signature(string Name, string? Title);

public sealed record CertificateRequest(
    string Template, IReadOnlyList<Guid> StudentIds, string? Title, string? Body, DateOnly? Date, IReadOnlyList<Signature>? Signatures, string? Primary, string? Accent, string? Language);

public sealed record CertificateItem(string Number, string Recipient, string Title, string Body, string DateText);

public sealed record CertificateStyle(string Layout, string Primary, string Accent, string Language, string CentreName, byte[]? Logo, IReadOnlyList<Signature> Signatures);

public sealed record CertificatePdf(byte[] Pdf, string FileName, IReadOnlyList<string> Numbers);

public sealed record IssuedDto(Guid Id, string TemplateId, string Number, Guid StudentId, string? StudentName, string Title, DateOnly IssuedOn, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record GetCertificateTemplatesQuery : IRequest<Result<IReadOnlyList<CertificateTemplate>>>;

public sealed record GenerateCertificatesCommand(CertificateRequest Request) : IRequest<Result<CertificatePdf>>;

public sealed record ListCertificatesQuery(Guid? StudentId, string? Cursor, int? Limit) : IRequest<Result<CursorPage<IssuedDto>>>;

/// <summary>Draws the certificates. Implemented outside this layer so no PDF library leaks in here.</summary>
public interface ICertificateRenderer
{
    byte[] Render(IReadOnlyList<CertificateItem> items, CertificateStyle style);
}

public static class CertificateCatalogue
{
    public static readonly IReadOnlyList<CertificateTemplate> All =
    [
        T("achievement-gold", "ACH", "Gold achievement", "إنجاز ذهبي", "border", "#92400E", "#D97706", "Certificate of Achievement", "شهادة إنجاز", "This certificate is proudly presented to {name} for outstanding achievement.", "تُمنح هذه الشهادة بكل فخر إلى {name} تقديراً لإنجازه المتميز."),
        T("star-student", "STAR", "Star student", "الطالب النجم", "playful", "#7C3AED", "#F59E0B", "Star Student", "الطالب النجم", "{name} shone brightly this term and is our star student.", "تألق {name} هذا الفصل وهو طالبنا النجم."),
        T("graduation", "GRAD", "Graduation", "التخرج", "classic", "#1E3A8A", "#B45309", "Certificate of Graduation", "شهادة تخرج", "{name} has successfully completed the nursery programme and is ready for the next adventure.", "أتم {name} برنامج الحضانة بنجاح وهو جاهز للمغامرة التالية."),
        T("good-behaviour", "BEH", "Good behaviour", "السلوك الحسن", "ribbon", "#047857", "#10B981", "Good Behaviour Award", "جائزة السلوك الحسن", "{name} is kind, helpful and a wonderful friend to everyone.", "{name} لطيف ومتعاون وصديق رائع للجميع."),
        T("reading-champion", "READ", "Reading champion", "بطل القراءة", "side", "#0E7490", "#06B6D4", "Reading Champion", "بطل القراءة", "{name} loves stories and has become a reading champion.", "يحب {name} القصص وأصبح بطلاً للقراءة."),
        T("artist", "ART", "Little artist", "الفنان الصغير", "playful", "#BE185D", "#F472B6", "Little Artist Award", "جائزة الفنان الصغير", "{name} paints the world with colour and imagination.", "يرسم {name} العالم بالألوان والخيال."),
        T("attendance", "ATT", "Perfect attendance", "الحضور المثالي", "modern", "#334155", "#0EA5E9", "Perfect Attendance", "الحضور المثالي", "{name} came every day, on time and ready to learn.", "حضر {name} كل يوم في الموعد وجاهزاً للتعلم."),
        T("participation", "PART", "Participation", "المشاركة", "ribbon", "#B45309", "#F59E0B", "Certificate of Participation", "شهادة مشاركة", "Thank you {name} for taking part with such enthusiasm.", "شكراً لك {name} على مشاركتك بكل حماس."),
        T("kindness", "KIND", "Kindness", "اللطف", "modern", "#9D174D", "#EC4899", "Kindness Award", "جائزة اللطف", "{name} shows kindness every day and makes our nursery a happier place.", "يُظهر {name} اللطف كل يوم ويجعل حضانتنا مكاناً أسعد."),
        T("sports-day", "SPORT", "Sports day", "يوم الرياضة", "side", "#15803D", "#84CC16", "Sports Day Medal", "ميدالية يوم الرياضة", "{name} ran, jumped and cheered with great spirit on sports day.", "ركض {name} وقفز وشجّع بروح رائعة في يوم الرياضة."),
    ];

    private static CertificateTemplate T(string id, string code, string en, string ar, string layout, string primary, string accent, string titleEn, string titleAr, string bodyEn, string bodyAr) =>
        new(id, code, new() { ["en"] = en, ["ar"] = ar }, layout, primary, accent, new() { ["en"] = titleEn, ["ar"] = titleAr }, new() { ["en"] = bodyEn, ["ar"] = bodyAr });
}

public sealed partial class GenerateCertificatesValidator : AbstractValidator<GenerateCertificatesCommand>
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    public GenerateCertificatesValidator()
    {
        RuleFor(x => x.Request.Template).Must(t => CertificateCatalogue.All.Any(c => c.Id == t)).WithMessage("Unknown template.");
        RuleFor(x => x.Request.StudentIds).Must(i => i is { Count: > 0 and <= 200 }).WithMessage("Choose between 1 and 200 children.");
        RuleFor(x => x.Request.Title).MaximumLength(150);
        RuleFor(x => x.Request.Body).MaximumLength(600);
        RuleFor(x => x.Request.Signatures).Must(s => s is null || (s.Count <= 3 && s.All(x => !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 80 && (x.Title?.Length ?? 0) <= 80))).WithMessage("Up to 3 signatures, each with a name.");
        RuleFor(x => x.Request.Primary).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use colours like #0E7C86.");
        RuleFor(x => x.Request.Accent).Must(c => string.IsNullOrWhiteSpace(c) || Hex().IsMatch(c)).WithMessage("Use colours like #0E7C86.");
        RuleFor(x => x.Request.Language).Must(l => l is null or "en" or "ar").WithMessage("Language must be en or ar.");
    }
}

public sealed class GetCertificateTemplatesHandler : IRequestHandler<GetCertificateTemplatesQuery, Result<IReadOnlyList<CertificateTemplate>>>
{
    public Task<Result<IReadOnlyList<CertificateTemplate>>> Handle(GetCertificateTemplatesQuery request, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<CertificateTemplate>>.Success(CertificateCatalogue.All));
}

/// <summary>Prints certificates for several children at once and records each number, so every certificate can be checked later.</summary>
public sealed class GenerateCertificatesHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, ICertificateRenderer renderer, CentreCalendar calendar)
    : IRequestHandler<GenerateCertificatesCommand, Result<CertificatePdf>>
{
    public async Task<Result<CertificatePdf>> Handle(GenerateCertificatesCommand command, CancellationToken ct)
    {
        var r = command.Request;
        var template = CertificateCatalogue.All.First(t => t.Id == r.Template);
        var language = r.Language ?? "en";
        var ids = r.StudentIds.Distinct().ToList();
        var kids = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ToListAsync(ct);
        if (kids.Count == 0)
        {
            return Error.NotFound("None of the chosen children were found.");
        }

        var centre = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Name).FirstAsync(ct);
        var settings = await db.CentreSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        byte[]? logo = settings?.LogoMediaId is { } logoId
            ? await db.MediaFiles.AsNoTracking().Where(m => m.Id == logoId && m.ContentType.StartsWith("image/")).Select(m => m.Content).FirstOrDefaultAsync(ct)
            : null;
        string Pick(Dictionary<string, string> text) => text.GetValueOrDefault(language) ?? text.GetValueOrDefault("en") ?? text.Values.First();

        var title = string.IsNullOrWhiteSpace(r.Title) ? Pick(template.DefaultTitle) : r.Title.Trim();
        var bodyTemplate = string.IsNullOrWhiteSpace(r.Body) ? Pick(template.DefaultBody) : r.Body.Trim();
        var date = r.Date ?? calendar.Today;
        var dateText = date.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

        // Take the next numbers for this design. Two people printing at once could pick the same number, so a clash is retried.
        List<CertificateIssued> issued = [];
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var last = await db.CertificatesIssued.AsNoTracking().Where(c => c.TemplateId == template.Id).MaxAsync(c => (int?)c.Number, ct) ?? 0;
            issued = kids.Select((k, i) => new CertificateIssued { TemplateId = template.Id, Number = last + i + 1, StudentId = k.Id, Title = title, IssuedOn = date, IssuedBy = current.UserId }).ToList();
            db.CertificatesIssued.AddRange(issued);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.CertificatesIssued.RemoveRange(issued);
            }
        }

        string Format(int n) => $"{template.Code}-{n:D5}";
        string RecipientName(Student k) => language == "ar" && !string.IsNullOrWhiteSpace(k.FirstNameArabic) ? $"{k.FirstNameArabic} {k.LastNameArabic}".Trim() : $"{k.FirstName} {k.LastName}";
        var items = kids.Select((k, i) => new CertificateItem(Format(issued[i].Number), RecipientName(k), title, bodyTemplate.Replace("{name}", RecipientName(k)), dateText)).ToList();
        var style = new CertificateStyle(template.Layout, r.Primary ?? template.Primary, r.Accent ?? template.Accent, language, Pick(centre), logo, r.Signatures ?? []);
        return Result<CertificatePdf>.Success(new CertificatePdf(renderer.Render(items, style), $"certificates-{template.Id}-{DateTime.UtcNow:yyyyMMdd}.pdf", items.Select(i => i.Number).ToList()));
    }
}

public sealed class ListCertificatesHandler(IAppDbContext db) : IRequestHandler<ListCertificatesQuery, Result<CursorPage<IssuedDto>>>
{
    public async Task<Result<CursorPage<IssuedDto>>> Handle(ListCertificatesQuery request, CancellationToken ct)
    {
        var q = db.CertificatesIssued.AsNoTracking().AsQueryable();
        if (request.StudentId is { } sid)
        {
            q = q.Where(c => c.StudentId == sid);
        }

        var page = await q.ToCursorPageAsync(request.Cursor, request.Limit,
            c => new IssuedDto(c.Id, c.TemplateId, string.Empty, c.StudentId, db.Students.Where(s => s.Id == c.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(), c.Title, c.IssuedOn, c.CreatedAt), ct);
        var numbers = await db.CertificatesIssued.AsNoTracking().Where(c => page.Items.Select(i => i.Id).Contains(c.Id)).Select(c => new { c.Id, c.TemplateId, c.Number }).ToListAsync(ct);
        string Code(string id) => CertificateCatalogue.All.FirstOrDefault(t => t.Id == id)?.Code ?? "CERT";
        var items = page.Items.Select(i => i with { Number = $"{Code(i.TemplateId)}-{numbers.First(n => n.Id == i.Id).Number:D5}" }).ToList();
        return Result<CursorPage<IssuedDto>>.Success(new CursorPage<IssuedDto>(items, page.NextCursor, page.HasMore, page.Limit));
    }
}
