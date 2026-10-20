using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ganss.Xss;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Ai;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Domain.CentreManagement;
using NMS.Domain.Communication;
using NMS.Domain.Notifications;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Newsletters;

public sealed record NewsletterFields(Dictionary<string, string> Title, Dictionary<string, string>? Body, string AudienceType, List<Guid>? AudienceIds);

public sealed record NewsletterDto(
    Guid Id, Dictionary<string, string> Title, string AudienceType, IReadOnlyList<Guid> AudienceIds, string Status, DateTimeOffset? ScheduledFor, DateTimeOffset? PublishedAt,
    int Recipients, int Opened, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record NewsletterDetailDto(NewsletterDto Newsletter, Dictionary<string, string> Body);

public sealed record MyNewsletterDto(Guid Id, Dictionary<string, string> Title, DateTimeOffset? PublishedAt, bool Opened, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ReadNewsletterDto(Guid Id, Dictionary<string, string> Title, string Body, string Language, DateTimeOffset? PublishedAt);

public sealed record LinkStat(int Index, string Url, int Clicks);

public sealed record NewsletterStatsDto(int Recipients, int Opened, double OpenRate, int Clicks, IReadOnlyList<LinkStat> Links);

public sealed record DraftDto(string Title, string Body, bool UsedAi);

public sealed record ListNewslettersQuery(string? Status, string? Search, Guid? CreatedBy, string? Cursor, int? Limit) : IRequest<Result<CursorPage<NewsletterDto>>>;

public sealed record GetNewsletterQuery(Guid Id) : IRequest<Result<NewsletterDetailDto>>;

public sealed record SaveNewsletterCommand(Guid? Id, NewsletterFields Fields) : IRequest<Result<NewsletterDetailDto>>;

public sealed record ScheduleNewsletterCommand(Guid Id, DateTimeOffset At) : IRequest<Result<NewsletterDetailDto>>;

public sealed record UnscheduleNewsletterCommand(Guid Id) : IRequest<Result<NewsletterDetailDto>>;

public sealed record PublishNewsletterCommand(Guid Id) : IRequest<Result<NewsletterDetailDto>>;

public sealed record ArchiveNewsletterCommand(Guid Id) : IRequest<Result<NewsletterDetailDto>>;

public sealed record DeleteNewsletterCommand(Guid Id) : IRequest<Result>;

public sealed record NewsletterStatsQuery(Guid Id) : IRequest<Result<NewsletterStatsDto>>;

public sealed record MyNewslettersQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<MyNewsletterDto>>>;

public sealed record ReadNewsletterCommand(Guid Id, string Language) : IRequest<Result<ReadNewsletterDto>>;

public sealed record ClickNewsletterCommand(string Token, int Index) : IRequest<Result<string>>;

public sealed record DraftNewsletterCommand(string Language) : IRequest<Result<DraftDto>>;

public sealed record RunNewsletterSendsCommand : IRequest<Result<int>>;

public sealed class SaveNewsletterValidator : AbstractValidator<SaveNewsletterCommand>
{
    public SaveNewsletterValidator()
    {
        RuleFor(x => x.Fields.Title).Must(t => t is not null && t.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en) && en.Length <= 200).WithMessage("Give the newsletter an English title (up to 200 characters).");
        RuleFor(x => x.Fields.AudienceType).Must(a => NewsletterAudiences.Kinds.Contains(a)).WithMessage("Unknown audience.");
        RuleFor(x => x.Fields).Must(f => f.AudienceType == NewsletterAudiences.All || f.AudienceIds is { Count: > 0 }).WithMessage("Choose who should receive it.");
        RuleFor(x => x.Fields.Body).Must(b => b is null || b.Values.All(v => v.Length <= 100_000)).WithMessage("The newsletter is too long.");
    }
}

/// <summary>Cleans newsletter HTML so only simple formatting, headings, lists and web links remain. Scripts, styles, images and event handlers are removed.</summary>
public static class NewsletterHtml
{
    private static readonly Regex Anchor = new("<a\\s[^>]*?href=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Clean(string? html)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "h2", "h3", "h4", "blockquote", "a" }) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer.Sanitize(html ?? string.Empty);
    }

    public static List<string> Links(string html) => Anchor.Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

    /// <summary>Points every link at the counting address for this reader, and opens it in a new tab.</summary>
    public static string Rewrite(string html, string token, int offset)
    {
        var i = offset;
        return Regex.Replace(html, "<a\\s([^>]*?)href=\"[^\"]*\"", _ => $"<a target=\"_blank\" rel=\"noopener noreferrer\" href=\"/api/v1/public/newsletters/click/{token}/{i++}\"", RegexOptions.IgnoreCase);
    }

    public static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<(br|/p|/li|/h\\d)[^>]*>", "\n", RegexOptions.IgnoreCase), "<[^>]+>", string.Empty)).Trim();
}

internal static class NewsletterMapping
{
    public static async Task<NewsletterDetailDto> DetailAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var n = await db.Newsletters.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var recipients = await db.NewsletterRecipients.CountAsync(r => r.NewsletterId == id, ct);
        var opened = await db.NewsletterRecipients.CountAsync(r => r.NewsletterId == id && r.OpenedAt != null, ct);
        return new NewsletterDetailDto(ToDto(n, recipients, opened), n.Body);
    }

    public static NewsletterDto ToDto(Newsletter n, int recipients, int opened) =>
        new(n.Id, n.Title, n.AudienceType, n.AudienceIds, n.Status, n.ScheduledFor, n.PublishedAt, recipients, opened, n.CreatedAt);

    public static async Task<Error?> CheckAudienceAsync(IAppDbContext db, string type, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (type == NewsletterAudiences.All)
        {
            return null;
        }

        var found = type switch
        {
            NewsletterAudiences.Room => await db.Rooms.CountAsync(x => ids.Contains(x.Id), ct),
            NewsletterAudiences.Program => await db.Programs.CountAsync(x => ids.Contains(x.Id), ct),
            NewsletterAudiences.Group => await db.StudentGroups.CountAsync(x => ids.Contains(x.Id), ct),
            NewsletterAudiences.Label => await db.Labels.CountAsync(x => ids.Contains(x.Id), ct),
            _ => 0,
        };
        return found == ids.Distinct().Count() ? null : Error.Validation("Choose existing rooms, programmes, groups or labels.", "audience_ids");
    }
}

public sealed class ListNewslettersHandler(IAppDbContext db) : IRequestHandler<ListNewslettersQuery, Result<CursorPage<NewsletterDto>>>
{
    public async Task<Result<CursorPage<NewsletterDto>>> Handle(ListNewslettersQuery request, CancellationToken ct)
    {
        var q = db.Newsletters.AsNoTracking().AsQueryable();
        if (request.Status is not null) q = q.Where(n => n.Status == request.Status);
        if (request.CreatedBy is { } by) q = q.Where(n => n.CreatedBy == by);
        var page = await q.ToCursorPageAsync(request.Cursor, request.Limit,
            n => new NewsletterDto(n.Id, n.Title, n.AudienceType, n.AudienceIds, n.Status, n.ScheduledFor, n.PublishedAt,
                db.NewsletterRecipients.Count(r => r.NewsletterId == n.Id), db.NewsletterRecipients.Count(r => r.NewsletterId == n.Id && r.OpenedAt != null), n.CreatedAt), ct);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var items = page.Items.Where(i => i.Title.Values.Any(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            return Result<CursorPage<NewsletterDto>>.Success(page with { Items = items });
        }

        return Result<CursorPage<NewsletterDto>>.Success(page);
    }
}

public sealed class GetNewsletterHandler(IAppDbContext db) : IRequestHandler<GetNewsletterQuery, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(GetNewsletterQuery request, CancellationToken ct) =>
        await db.Newsletters.AnyAsync(n => n.Id == request.Id, ct) ? Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, request.Id, ct)) : Error.NotFound("Newsletter not found.");
}

public sealed class SaveNewsletterHandler(IAppDbContext db) : IRequestHandler<SaveNewsletterCommand, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(SaveNewsletterCommand request, CancellationToken ct)
    {
        var f = request.Fields;
        var ids = (f.AudienceIds ?? []).Distinct().ToList();
        if (await NewsletterMapping.CheckAudienceAsync(db, f.AudienceType, ids, ct) is { } bad)
        {
            return bad;
        }

        Newsletter n;
        if (request.Id is { } id)
        {
            var found = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Newsletter not found.");
            }

            if (found.Status is NewsletterStatuses.Published or NewsletterStatuses.Archived)
            {
                return Error.InvalidState("A sent newsletter cannot be changed.");
            }

            n = found;
        }
        else
        {
            n = new Newsletter();
            db.Newsletters.Add(n);
        }

        n.Title = f.Title.Where(t => !string.IsNullOrWhiteSpace(t.Value)).ToDictionary(t => t.Key, t => t.Value.Trim());
        n.Body = (f.Body ?? []).Select(b => (b.Key, Html: NewsletterHtml.Clean(b.Value))).Where(b => NewsletterHtml.Text(b.Html).Length > 0).ToDictionary(b => b.Key, b => b.Html);
        n.AudienceType = f.AudienceType;
        n.AudienceIds = f.AudienceType == NewsletterAudiences.All ? [] : ids;
        await db.SaveChangesAsync(ct);
        return Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, n.Id, ct));
    }
}

public sealed class ScheduleNewsletterHandler(IAppDbContext db, IClock clock) : IRequestHandler<ScheduleNewsletterCommand, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(ScheduleNewsletterCommand request, CancellationToken ct)
    {
        var n = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (n is null)
        {
            return Error.NotFound("Newsletter not found.");
        }

        if (n.Status is not (NewsletterStatuses.Draft or NewsletterStatuses.Scheduled))
        {
            return Error.InvalidState("Only a draft can be scheduled.");
        }

        if (!n.Body.TryGetValue("en", out var en) || NewsletterHtml.Text(en).Length == 0)
        {
            return Error.Validation("Write the English newsletter before scheduling it.", "body");
        }

        if (request.At < clock.UtcNow.AddMinutes(1))
        {
            return Error.Validation("Choose a time in the future.", "at");
        }

        n.Status = NewsletterStatuses.Scheduled;
        n.ScheduledFor = request.At;
        await db.SaveChangesAsync(ct);
        return Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, n.Id, ct));
    }
}

public sealed class UnscheduleNewsletterHandler(IAppDbContext db) : IRequestHandler<UnscheduleNewsletterCommand, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(UnscheduleNewsletterCommand request, CancellationToken ct)
    {
        var n = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == request.Id && x.Status == NewsletterStatuses.Scheduled, ct);
        if (n is null)
        {
            return Error.NotFound("No scheduled newsletter found.");
        }

        n.Status = NewsletterStatuses.Draft;
        n.ScheduledFor = null;
        await db.SaveChangesAsync(ct);
        return Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, n.Id, ct));
    }
}

/// <summary>Sends a newsletter: works out who should get it, gives each parent a private token, saves the links so clicks can be counted, and notifies them.</summary>
public sealed class NewsletterSender(IAppDbContext db, Notifier notifier, IClock clock, ITenantContext tenant)
{
    public async Task<Result> SendAsync(Newsletter n, CancellationToken ct)
    {
        if (!n.Body.TryGetValue("en", out var en) || NewsletterHtml.Text(en).Length == 0)
        {
            return Result.Failure(Error.Validation("Write the English newsletter before sending it.", "body"));
        }

        var kids = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
        var ids = n.AudienceIds;
        kids = n.AudienceType switch
        {
            NewsletterAudiences.Room => kids.Where(s => s.RoomId != null && ids.Contains(s.RoomId.Value)),
            NewsletterAudiences.Program => kids.Where(s => s.ProgramId != null && ids.Contains(s.ProgramId.Value)),
            NewsletterAudiences.Group => kids.Where(s => s.GroupId != null && ids.Contains(s.GroupId.Value)),
            NewsletterAudiences.Label => kids.Where(s => db.StudentLabels.Any(l => l.StudentId == s.Id && ids.Contains(l.LabelId))),
            _ => kids,
        };
        var kidIds = await kids.Select(s => s.Id).ToListAsync(ct);
        var users = await db.StudentGuardians.AsNoTracking()
            .Where(sg => kidIds.Contains(sg.StudentId) && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
            .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);

        var enLinks = NewsletterHtml.Links(en);
        var arLinks = n.Body.TryGetValue("ar", out var ar) ? NewsletterHtml.Links(ar) : [];
        var all = enLinks.Concat(arLinks).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            db.NewsletterLinks.Add(new NewsletterLink { NewsletterId = n.Id, LinkIndex = i, Url = all[i] });
        }

        foreach (var user in users)
        {
            db.NewsletterRecipients.Add(new NewsletterRecipient { NewsletterId = n.Id, UserId = user, Token = $"{tenant.TenantId:N}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}" });
        }

        n.Status = NewsletterStatuses.Published;
        n.PublishedAt = clock.UtcNow;
        n.ScheduledFor = null;
        await db.SaveChangesAsync(ct);

        var preview = NewsletterHtml.Text(en);
        var arText = n.Body.TryGetValue("ar", out var arBody) ? NewsletterHtml.Text(arBody) : null;
        await notifier.NotifyAsync(users, NotificationTiers.Informational, "newsletter",
            new() { ["en"] = n.Title.GetValueOrDefault("en") ?? "Newsletter", ["ar"] = n.Title.GetValueOrDefault("ar") ?? n.Title.GetValueOrDefault("en") ?? "نشرة" },
            new() { ["en"] = preview.Length > 300 ? preview[..300] + "..." : preview, ["ar"] = arText is null ? (preview.Length > 300 ? preview[..300] + "..." : preview) : (arText.Length > 300 ? arText[..300] + "..." : arText) }, "/newsletters", ct);
        return Result.Success();
    }
}

public sealed class PublishNewsletterHandler(IAppDbContext db, NewsletterSender sender) : IRequestHandler<PublishNewsletterCommand, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(PublishNewsletterCommand request, CancellationToken ct)
    {
        var n = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (n is null)
        {
            return Error.NotFound("Newsletter not found.");
        }

        if (n.Status is NewsletterStatuses.Published or NewsletterStatuses.Archived)
        {
            return Error.InvalidState("This newsletter has already been sent.");
        }

        var sent = await sender.SendAsync(n, ct);
        return sent.IsFailure ? Result<NewsletterDetailDto>.Failure(sent.Errors) : Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, n.Id, ct));
    }
}

public sealed class ArchiveNewsletterHandler(IAppDbContext db) : IRequestHandler<ArchiveNewsletterCommand, Result<NewsletterDetailDto>>
{
    public async Task<Result<NewsletterDetailDto>> Handle(ArchiveNewsletterCommand request, CancellationToken ct)
    {
        var n = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (n is null)
        {
            return Error.NotFound("Newsletter not found.");
        }

        if (n.Status != NewsletterStatuses.Published)
        {
            return Error.InvalidState("Only a sent newsletter can be archived.");
        }

        n.Status = NewsletterStatuses.Archived;
        await db.SaveChangesAsync(ct);
        return Result<NewsletterDetailDto>.Success(await NewsletterMapping.DetailAsync(db, n.Id, ct));
    }
}

public sealed class DeleteNewsletterHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteNewsletterCommand, Result>
{
    public async Task<Result> Handle(DeleteNewsletterCommand request, CancellationToken ct)
    {
        var n = await db.Newsletters.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (n is null)
        {
            return Result.Failure(Error.NotFound("Newsletter not found."));
        }

        if (n.Status is not (NewsletterStatuses.Draft or NewsletterStatuses.Scheduled))
        {
            return Result.Failure(Error.InvalidState("A sent newsletter is kept as a record. Archive it instead."));
        }

        n.IsDeleted = true;
        n.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class NewsletterStatsHandler(IAppDbContext db) : IRequestHandler<NewsletterStatsQuery, Result<NewsletterStatsDto>>
{
    public async Task<Result<NewsletterStatsDto>> Handle(NewsletterStatsQuery request, CancellationToken ct)
    {
        if (!await db.Newsletters.AnyAsync(n => n.Id == request.Id, ct))
        {
            return Error.NotFound("Newsletter not found.");
        }

        var recipients = await db.NewsletterRecipients.CountAsync(r => r.NewsletterId == request.Id, ct);
        var opened = await db.NewsletterRecipients.CountAsync(r => r.NewsletterId == request.Id && r.OpenedAt != null, ct);
        var clicks = await db.NewsletterClicks.AsNoTracking().Where(c => c.NewsletterId == request.Id).GroupBy(c => c.LinkIndex).Select(g => new { Index = g.Key, N = g.Count() }).ToListAsync(ct);
        var links = await db.NewsletterLinks.AsNoTracking().Where(l => l.NewsletterId == request.Id).OrderBy(l => l.LinkIndex).ToListAsync(ct);
        return Result<NewsletterStatsDto>.Success(new NewsletterStatsDto(recipients, opened, recipients == 0 ? 0 : Math.Round(opened * 100.0 / recipients, 1), clicks.Sum(c => c.N),
            links.Select(l => new LinkStat(l.LinkIndex, l.Url, clicks.FirstOrDefault(c => c.Index == l.LinkIndex)?.N ?? 0)).ToList()));
    }
}

public sealed class MyNewslettersHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<MyNewslettersQuery, Result<CursorPage<MyNewsletterDto>>>
{
    public async Task<Result<CursorPage<MyNewsletterDto>>> Handle(MyNewslettersQuery request, CancellationToken ct)
    {
        var me = current.UserId;
        var mine = db.Newsletters.AsNoTracking().Where(n => n.Status != NewsletterStatuses.Draft && n.Status != NewsletterStatuses.Scheduled && db.NewsletterRecipients.Any(r => r.NewsletterId == n.Id && r.UserId == me));
        return Result<CursorPage<MyNewsletterDto>>.Success(await mine.ToCursorPageAsync(request.Cursor, request.Limit,
            n => new MyNewsletterDto(n.Id, n.Title, n.PublishedAt, db.NewsletterRecipients.Any(r => r.NewsletterId == n.Id && r.UserId == me && r.OpenedAt != null), n.CreatedAt), ct));
    }
}

public sealed class ReadNewsletterHandler(IAppDbContext db, ICurrentUser current, IClock clock) : IRequestHandler<ReadNewsletterCommand, Result<ReadNewsletterDto>>
{
    public async Task<Result<ReadNewsletterDto>> Handle(ReadNewsletterCommand request, CancellationToken ct)
    {
        var me = current.UserId;
        var recipient = await db.NewsletterRecipients.FirstOrDefaultAsync(r => r.NewsletterId == request.Id && r.UserId == me, ct);
        var n = await db.Newsletters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id && (x.Status == NewsletterStatuses.Published || x.Status == NewsletterStatuses.Archived), ct);
        if (recipient is null || n is null)
        {
            return Error.NotFound("Newsletter not found.");
        }

        if (recipient.OpenedAt is null)
        {
            recipient.OpenedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var language = n.Body.ContainsKey(request.Language) ? request.Language : "en";
        var offset = language == "ar" && n.Body.TryGetValue("en", out var en) ? NewsletterHtml.Links(en).Count : 0;
        return Result<ReadNewsletterDto>.Success(new ReadNewsletterDto(n.Id, n.Title, NewsletterHtml.Rewrite(n.Body[language], recipient.Token, offset), language, n.PublishedAt));
    }
}

/// <summary>A click from a newsletter. Only addresses saved when it was sent can be opened, so this can never be used to send people somewhere else.</summary>
public sealed class ClickNewsletterHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<ClickNewsletterCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ClickNewsletterCommand request, CancellationToken ct)
    {
        // The token starts with the centre it belongs to, so the right centre's records are searched.
        var dot = request.Token.IndexOf('.');
        if (dot != 32 || !Guid.TryParseExact(request.Token[..32], "N", out var tenantId))
        {
            return Error.NotFound("Link not found.");
        }

        tenant.TenantId = tenantId;
        var recipient = await db.NewsletterRecipients.AsNoTracking().FirstOrDefaultAsync(r => r.Token == request.Token, ct);
        if (recipient is null)
        {
            return Error.NotFound("Link not found.");
        }

        var link = await db.NewsletterLinks.AsNoTracking().FirstOrDefaultAsync(l => l.NewsletterId == recipient.NewsletterId && l.LinkIndex == request.Index, ct);
        if (link is null)
        {
            return Error.NotFound("Link not found.");
        }

        db.NewsletterClicks.Add(new NewsletterClick { NewsletterId = recipient.NewsletterId, RecipientId = recipient.Id, LinkIndex = request.Index });
        await db.SaveChangesAsync(ct);
        return Result<string>.Success(link.Url);
    }
}

public sealed class DraftNewsletterHandler(IAppDbContext db, IAiBrain ai, CentreCalendar calendar, IAccessScope scope) : IRequestHandler<DraftNewsletterCommand, Result<DraftDto>>
{
    public async Task<Result<DraftDto>> Handle(DraftNewsletterCommand request, CancellationToken ct)
    {
        var lang = request.Language == "ar" ? "ar" : "en";
        _ = scope;
        var since = calendar.Now.AddDays(-7);
        var posts = await db.ActivityPosts.AsNoTracking().Where(a => a.CreatedAt >= since).GroupBy(a => a.Type).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var from = calendar.Today;
        var to = from.AddDays(14);
        var windowStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var windowEnd = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);
        var events = await db.CalendarEvents.AsNoTracking().Where(e => e.Visibility == "parents" && e.Type != "staff_only" && e.StartsAt >= windowStart && e.StartsAt <= windowEnd).OrderBy(e => e.StartsAt).Take(8).ToListAsync(ct);
        var eventLines = events.Select(e => $"{TimeZoneInfo.ConvertTime(e.StartsAt, calendar.Zone):d MMM}: {e.Title.GetValueOrDefault(lang) ?? e.Title.GetValueOrDefault("en")}").ToList();
        var postLines = posts.Select(p => $"{p.N} {p.Key}").ToList();
        var title = lang == "ar" ? "أخبار الحضانة هذا الأسبوع" : "This week at the nursery";

        // Only totals and event titles go to the AI: no child names, photos or messages.
        if (ai.IsConfigured)
        {
            var facts = $"Posts shared with parents this week (counts by type): {(postLines.Count == 0 ? "none" : string.Join(", ", postLines))}. Upcoming events: {(eventLines.Count == 0 ? "none" : string.Join("; ", eventLines))}.";
            var reply = await ai.CompleteAsync(
                $"You write short, warm newsletters for parents of a nursery in the UAE. Write in {(lang == "ar" ? "Arabic" : "English")}. Use only the facts given, never invent names or details. Reply with simple HTML using only <h2>, <p>, <ul>, <li>, <strong>.",
                facts, ct);
            if (reply.IsSuccess && !string.IsNullOrWhiteSpace(reply.Value))
            {
                var clean = NewsletterHtml.Clean(reply.Value);
                if (!Regex.IsMatch(clean, "<(p|h2|h3|ul|ol)[ >]"))
                {
                    // The assistant answered in plain text: turn its paragraphs into paragraphs.
                    clean = "<p>" + string.Join("</p><p>", WebUtility.HtmlEncode(reply.Value.Trim()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Replace("\n", "<br>"))) + "</p>";
                }

                return Result<DraftDto>.Success(new DraftDto(title, clean, true));
            }
        }

        var body = lang == "ar"
            ? $"<h2>{title}</h2><p>شاركنا هذا الأسبوع {posts.Sum(p => p.N)} منشوراً مع أولياء الأمور.</p>{(eventLines.Count == 0 ? string.Empty : "<h3>قريباً</h3><ul>" + string.Join(string.Empty, eventLines.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>")}<p>شكراً لكم على دعمكم.</p>"
            : $"<h2>{title}</h2><p>We shared {posts.Sum(p => p.N)} updates with parents this week.</p>{(eventLines.Count == 0 ? string.Empty : "<h3>Coming up</h3><ul>" + string.Join(string.Empty, eventLines.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>")}<p>Thank you for your support.</p>";
        return Result<DraftDto>.Success(new DraftDto(title, NewsletterHtml.Clean(body), false));
    }
}

/// <summary>Sends newsletters whose time has come. Runs with the other daily jobs.</summary>
public sealed class RunNewsletterSendsHandler(IAppDbContext db, NewsletterSender sender, IClock clock) : IRequestHandler<RunNewsletterSendsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunNewsletterSendsCommand request, CancellationToken ct)
    {
        var due = await db.Newsletters.Where(n => n.Status == NewsletterStatuses.Scheduled && n.ScheduledFor <= clock.UtcNow).ToListAsync(ct);
        var sent = 0;
        foreach (var n in due)
        {
            if ((await sender.SendAsync(n, ct)).IsSuccess)
            {
                sent++;
            }
            else
            {
                n.Status = NewsletterStatuses.Draft;
                n.ScheduledFor = null;
                await db.SaveChangesAsync(ct);
            }
        }

        return Result<int>.Success(sent);
    }
}
