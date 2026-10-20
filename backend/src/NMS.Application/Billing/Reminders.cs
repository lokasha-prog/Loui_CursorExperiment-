using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Domain.Billing;
using NMS.Domain.Notifications;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Billing;

public sealed record BillingSettingsDto(
    int DueDays, bool RemindersEnabled, IReadOnlyList<int> BeforeDueDays, IReadOnlyList<int> AfterDueDays,
    Dictionary<string, string> BeforeSubject, Dictionary<string, string> BeforeBody,
    Dictionary<string, string> OverdueSubject, Dictionary<string, string> OverdueBody);

public sealed record GetBillingSettingsQuery : IRequest<Result<BillingSettingsDto>>;

public sealed record SaveBillingSettingsCommand(
    int DueDays, bool RemindersEnabled, IReadOnlyList<int> BeforeDueDays, IReadOnlyList<int> AfterDueDays,
    Dictionary<string, string>? BeforeSubject, Dictionary<string, string>? BeforeBody,
    Dictionary<string, string>? OverdueSubject, Dictionary<string, string>? OverdueBody) : IRequest<Result<BillingSettingsDto>>;

public sealed record ReminderLogDto(Guid Id, Guid InvoiceId, string InvoiceNumber, string? StudentName, string Kind, int OffsetDays, int Recipients, string Status, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListRemindersQuery(string? Cursor, int? Limit) : IRequest<Result<CursorPage<ReminderLogDto>>>;

public sealed record RunRemindersCommand : IRequest<Result<int>>;

/// <summary>The wording used until a centre writes its own. Variables in braces are filled in when a reminder is sent.</summary>
public static class ReminderDefaults
{
    public static readonly string[] Variables = ["{child}", "{invoice}", "{amount}", "{due}"];

    public static Dictionary<string, string> BeforeSubject => new() { ["en"] = "Fee reminder: invoice {invoice} is due {due}", ["ar"] = "تذكير بالرسوم: الفاتورة {invoice} مستحقة في {due}" };

    public static Dictionary<string, string> BeforeBody => new()
    {
        ["en"] = "Dear parent, a reminder that AED {amount} for {child} is due on {due} (invoice {invoice}). Thank you.",
        ["ar"] = "عزيزي ولي الأمر، نذكركم بأن مبلغ {amount} درهم عن {child} مستحق في {due} (الفاتورة {invoice}). شكراً لكم.",
    };

    public static Dictionary<string, string> OverdueSubject => new() { ["en"] = "Overdue: invoice {invoice}", ["ar"] = "متأخرة: الفاتورة {invoice}" };

    public static Dictionary<string, string> OverdueBody => new()
    {
        ["en"] = "Dear parent, AED {amount} for {child} was due on {due} (invoice {invoice}) and is still unpaid. Please pay or contact the centre.",
        ["ar"] = "عزيزي ولي الأمر، مبلغ {amount} درهم عن {child} كان مستحقاً في {due} (الفاتورة {invoice}) ولم يُسدد بعد. يرجى السداد أو التواصل مع المركز.",
    };

    public static Dictionary<string, string> Fill(Dictionary<string, string> template, string child, string invoice, string amount, string due) =>
        template.ToDictionary(t => t.Key, t => t.Value.Replace("{child}", child).Replace("{invoice}", invoice).Replace("{amount}", amount).Replace("{due}", due));
}

/// <summary>What billing actually uses for this centre: its saved settings, or the defaults from configuration when it has not saved any.</summary>
public sealed class BillingSettingsReader(IAppDbContext db, IOptions<BillingOptions> options)
{
    public static IReadOnlyList<int> ParseDays(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : -1).Where(n => n is >= 0 and <= 365).Distinct().OrderDescending().ToList();

    public async Task<BillingSettingsDto> GetAsync(CancellationToken ct)
    {
        var row = await db.BillingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return row is null
            ? new BillingSettingsDto(options.Value.DefaultDueDays, true, [3, 1], [1, 7], ReminderDefaults.BeforeSubject, ReminderDefaults.BeforeBody, ReminderDefaults.OverdueSubject, ReminderDefaults.OverdueBody)
            : new BillingSettingsDto(
                row.DueDays, row.RemindersEnabled, ParseDays(row.BeforeDueDays), ParseDays(row.AfterDueDays),
                row.BeforeSubject ?? ReminderDefaults.BeforeSubject, row.BeforeBody ?? ReminderDefaults.BeforeBody,
                row.OverdueSubject ?? ReminderDefaults.OverdueSubject, row.OverdueBody ?? ReminderDefaults.OverdueBody);
    }
}

public sealed class SaveBillingSettingsValidator : AbstractValidator<SaveBillingSettingsCommand>
{
    private static bool HasEnglish(Dictionary<string, string>? text) => text is null || (text.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en));

    public SaveBillingSettingsValidator()
    {
        RuleFor(x => x.DueDays).InclusiveBetween(0, 365);
        RuleFor(x => x.BeforeDueDays).Must(d => d is not null && d.Count <= 10 && d.All(n => n is >= 1 and <= 60)).WithMessage("Choose up to 10 lead times between 1 and 60 days.");
        RuleFor(x => x.AfterDueDays).Must(d => d is not null && d.Count <= 10 && d.All(n => n is >= 1 and <= 120)).WithMessage("Choose up to 10 follow-up days between 1 and 120.");
        RuleFor(x => x.BeforeSubject).Must(HasEnglish).WithMessage("The English wording is required.");
        RuleFor(x => x.BeforeBody).Must(HasEnglish).WithMessage("The English wording is required.");
        RuleFor(x => x.OverdueSubject).Must(HasEnglish).WithMessage("The English wording is required.");
        RuleFor(x => x.OverdueBody).Must(HasEnglish).WithMessage("The English wording is required.");
    }
}

public sealed class GetBillingSettingsHandler(BillingSettingsReader reader) : IRequestHandler<GetBillingSettingsQuery, Result<BillingSettingsDto>>
{
    public async Task<Result<BillingSettingsDto>> Handle(GetBillingSettingsQuery request, CancellationToken ct) => Result<BillingSettingsDto>.Success(await reader.GetAsync(ct));
}

public sealed class SaveBillingSettingsHandler(IAppDbContext db, BillingSettingsReader reader) : IRequestHandler<SaveBillingSettingsCommand, Result<BillingSettingsDto>>
{
    public async Task<Result<BillingSettingsDto>> Handle(SaveBillingSettingsCommand request, CancellationToken ct)
    {
        var row = await db.BillingSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new BillingSettings();
            db.BillingSettings.Add(row);
        }

        row.DueDays = request.DueDays;
        row.RemindersEnabled = request.RemindersEnabled;
        row.BeforeDueDays = string.Join(',', request.BeforeDueDays.Distinct().OrderDescending());
        row.AfterDueDays = string.Join(',', request.AfterDueDays.Distinct().OrderDescending());
        row.BeforeSubject = request.BeforeSubject;
        row.BeforeBody = request.BeforeBody;
        row.OverdueSubject = request.OverdueSubject;
        row.OverdueBody = request.OverdueBody;
        await db.SaveChangesAsync(ct);
        return Result<BillingSettingsDto>.Success(await reader.GetAsync(ct));
    }
}

public sealed class ListRemindersHandler(IAppDbContext db) : IRequestHandler<ListRemindersQuery, Result<CursorPage<ReminderLogDto>>>
{
    public async Task<Result<CursorPage<ReminderLogDto>>> Handle(ListRemindersQuery request, CancellationToken ct) =>
        Result<CursorPage<ReminderLogDto>>.Success(await db.ReminderLogs.AsNoTracking().ToCursorPageAsync(request.Cursor, request.Limit,
            r => new ReminderLogDto(
                r.Id, r.InvoiceId, db.Invoices.Where(i => i.Id == r.InvoiceId).Select(i => i.InvoiceNumber).First(),
                db.Invoices.Where(i => i.Id == r.InvoiceId).Select(i => db.Students.Where(s => s.Id == i.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault()).First(),
                r.Kind, r.OffsetDays, r.Recipients, r.Status, r.CreatedAt), ct));
}

/// <summary>
/// The daily reminder run (spec 03 §8.4): tells the family a set number of days before the due date and again after it passes.
/// Before-due reminders are informational and parents may switch them off; overdue notices are operational and are not optional.
/// Each reminder is logged once, so running the job again never repeats it.
/// </summary>
public sealed class RunRemindersHandler(IAppDbContext db, BillingSettingsReader reader, Notifier notifier, CentreCalendar calendar) : IRequestHandler<RunRemindersCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunRemindersCommand request, CancellationToken ct)
    {
        var settings = await reader.GetAsync(ct);
        if (!settings.RemindersEnabled)
        {
            return Result<int>.Success(0);
        }

        var today = calendar.Today;
        var open = await db.Invoices.AsNoTracking()
            .Where(i => (i.Status == InvoiceStatuses.Issued || i.Status == InvoiceStatuses.PartiallyPaid || i.Status == InvoiceStatuses.Overdue) && i.TotalFils > i.PaidFils)
            .ToListAsync(ct);
        var sent = 0;
        foreach (var invoice in open)
        {
            var untilDue = invoice.DueDate.DayNumber - today.DayNumber;
            var (kind, offset) = untilDue >= 0 ? (ReminderKinds.BeforeDue, untilDue) : (ReminderKinds.Overdue, -untilDue);
            var wanted = kind == ReminderKinds.BeforeDue ? settings.BeforeDueDays : settings.AfterDueDays;
            if (!wanted.Contains(offset) || await db.ReminderLogs.AnyAsync(r => r.InvoiceId == invoice.Id && r.Kind == kind && r.OffsetDays == offset, ct))
            {
                continue;
            }

            var name = await db.Students.AsNoTracking().Where(s => s.Id == invoice.StudentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) ?? string.Empty;
            var users = await db.StudentGuardians.AsNoTracking()
                .Where(sg => sg.StudentId == invoice.StudentId && sg.Guardian!.AppUserId != null && sg.Guardian.ReceivesCommunications)
                .Select(sg => sg.Guardian!.AppUserId!.Value).Distinct().ToListAsync(ct);
            var amount = (invoice.OutstandingFils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            var due = invoice.DueDate.ToString("yyyy-MM-dd");
            var before = kind == ReminderKinds.BeforeDue;
            await notifier.NotifyAsync(users, before ? NotificationTiers.Informational : NotificationTiers.Operational, before ? "fee_reminder" : "fee_overdue",
                ReminderDefaults.Fill(before ? settings.BeforeSubject : settings.OverdueSubject, name, invoice.InvoiceNumber, amount, due),
                ReminderDefaults.Fill(before ? settings.BeforeBody : settings.OverdueBody, name, invoice.InvoiceNumber, amount, due), "/billing", ct);
            db.ReminderLogs.Add(new ReminderLog { InvoiceId = invoice.Id, Kind = kind, OffsetDays = offset, Recipients = users.Count, Status = users.Count == 0 ? "no_recipients" : "sent" });
            await db.SaveChangesAsync(ct);
            sent++;
        }

        return Result<int>.Success(sent);
    }
}
