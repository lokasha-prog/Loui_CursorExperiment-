using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Domain.Calendar;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.Calendar;

public sealed record FeedLinkDto(string Path);

public sealed record GetFeedLinkQuery(bool Rotate) : IRequest<Result<FeedLinkDto>>;

public sealed record IcsFeedQuery(string Centre, string Token) : IRequest<Result<string>>;

/// <summary>The link families add to their phone calendar. The secret in it can be replaced to switch the old link off.</summary>
public sealed class GetFeedLinkHandler(IAppDbContext db, ITenantContext tenant) : IRequestHandler<GetFeedLinkQuery, Result<FeedLinkDto>>
{
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public async Task<Result<FeedLinkDto>> Handle(GetFeedLinkQuery request, CancellationToken ct)
    {
        var row = await db.CentreSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new CentreSettings();
            db.CentreSettings.Add(row);
        }

        if (request.Rotate || string.IsNullOrEmpty(row.CalendarFeedToken))
        {
            row.CalendarFeedToken = NewToken();
            await db.SaveChangesAsync(ct);
        }

        var slug = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Slug).FirstAsync(ct);
        return Result<FeedLinkDto>.Success(new FeedLinkDto($"/api/v1/public/calendar/{slug}/{row.CalendarFeedToken}.ics"));
    }
}

/// <summary>Builds the subscription calendar (RFC 5545) of everything parents may see, from last month to a year ahead.</summary>
public sealed class IcsFeedHandler(IAppDbContext db, ITenantContext tenant, IClock clock) : IRequestHandler<IcsFeedQuery, Result<string>>
{
    public async Task<Result<string>> Handle(IcsFeedQuery request, CancellationToken ct)
    {
        var slug = request.Centre.Trim().ToLowerInvariant();
        var centre = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => !t.IsDeleted && t.Status == "active" && t.Slug == slug, ct);
        if (centre is null)
        {
            return Error.NotFound("Calendar not found.");
        }

        tenant.TenantId = centre.Id;
        var stored = await db.CentreSettings.AsNoTracking().Select(s => s.CalendarFeedToken).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(stored) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(request.Token)))
        {
            return Error.NotFound("Calendar not found.");
        }

        var now = clock.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddYears(1);
        var events = await db.CalendarEvents.AsNoTracking()
            .Where(e => e.Visibility == "parents" && e.Type != EventTypes.StaffOnly && (e.Recurrence != null || (e.EndsAt >= from && e.StartsAt <= to))).ToListAsync(ct);

        var sb = new StringBuilder();
        var name = centre.Name.GetValueOrDefault("en") ?? slug;
        Line(sb, "BEGIN:VCALENDAR");
        Line(sb, "VERSION:2.0");
        Line(sb, "PRODID:-//NMS//Nursery Calendar//EN");
        Line(sb, "CALSCALE:GREGORIAN");
        Line(sb, $"X-WR-CALNAME:{Escape(name)}");
        foreach (var e in events)
        {
            Line(sb, "BEGIN:VEVENT");
            Line(sb, $"UID:{e.Id}@nms");
            Line(sb, $"DTSTAMP:{Utc(now)}");
            if (e.IsAllDay)
            {
                Line(sb, $"DTSTART;VALUE=DATE:{e.StartsAt:yyyyMMdd}");
                Line(sb, $"DTEND;VALUE=DATE:{e.EndsAt.AddDays(1):yyyyMMdd}");
            }
            else
            {
                Line(sb, $"DTSTART:{Utc(e.StartsAt)}");
                Line(sb, $"DTEND:{Utc(e.EndsAt)}");
            }

            Line(sb, $"SUMMARY:{Escape(e.Title.GetValueOrDefault("en") ?? e.Title.Values.FirstOrDefault() ?? string.Empty)}");
            if (e.Title.TryGetValue("ar", out var ar))
            {
                Line(sb, $"X-TITLE-AR:{Escape(ar)}");
            }

            if (e.Description is { } d && (d.GetValueOrDefault("en") ?? d.Values.FirstOrDefault()) is { } text)
            {
                Line(sb, $"DESCRIPTION:{Escape(text)}");
            }

            if (e.Type == EventTypes.Holiday)
            {
                Line(sb, "CATEGORIES:Holiday");
            }

            if (!string.IsNullOrWhiteSpace(e.Recurrence))
            {
                Line(sb, $"RRULE:{e.Recurrence}");
            }

            Line(sb, "END:VEVENT");
        }

        Line(sb, "END:VCALENDAR");
        return Result<string>.Success(sb.ToString());
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    private static string Utc(DateTimeOffset d) => d.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>Lines longer than 75 bytes are folded with a leading space on the next line (RFC 5545 section 3.1).</summary>
    private static void Line(StringBuilder sb, string text)
    {
        var size = 0;
        var limit = 75;
        foreach (var rune in text.EnumerateRunes())
        {
            var n = rune.Utf8SequenceLength;
            if (size + n > limit)
            {
                sb.Append("\r\n ");
                size = 0;
                limit = 74;
            }

            sb.Append(rune.ToString());
            size += n;
        }

        sb.Append("\r\n");
    }
}
