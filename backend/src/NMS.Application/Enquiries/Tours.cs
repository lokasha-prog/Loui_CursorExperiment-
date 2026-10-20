using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Common;
using NMS.Application.Notifications;
using NMS.Domain.Admissions;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Enquiries;

public sealed record TourFields(Guid EnquiryId, DateTimeOffset StartsAt, int? DurationMinutes, Guid? StaffId, string? TourType, int? Adults, int? Children, string? Notes);

public sealed record TourDto(
    Guid Id, Guid EnquiryId, string FamilyName, string ChildName, Guid? StaffId, string? StaffName, DateTimeOffset StartsAt, int DurationMinutes,
    string TourType, int Adults, int Children, string? Notes, string Status);

public sealed record BlockOffDto(Guid Id, Guid? StaffId, string? StaffName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

public sealed record TaskDto(Guid Id, string Title, DateOnly DueDate, Guid? AssignedStaffId, string? AssignedStaffName, Guid? EnquiryId, string? FamilyName, bool IsDone, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record ListToursQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? StaffId, string? Status) : IRequest<Result<IReadOnlyList<TourDto>>>;

public sealed record SaveTourCommand(Guid? Id, TourFields Fields) : IRequest<Result<TourDto>>;

public sealed record SetTourStatusCommand(Guid Id, string Status) : IRequest<Result<TourDto>>;

public sealed record ListBlockOffsQuery(DateTimeOffset? From, DateTimeOffset? To) : IRequest<Result<IReadOnlyList<BlockOffDto>>>;

public sealed record AddBlockOffCommand(Guid? StaffId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason) : IRequest<Result<BlockOffDto>>;

public sealed record DeleteBlockOffCommand(Guid Id) : IRequest<Result>;

public sealed record ListTasksQuery(bool Done, Guid? StaffId, string? Cursor, int? Limit) : IRequest<Result<CursorPage<TaskDto>>>;

public sealed record CompleteTaskCommand(Guid Id) : IRequest<Result<TaskDto>>;

public sealed record RunTourRemindersCommand : IRequest<Result<int>>;

public sealed record RunOutboundEmailCommand : IRequest<Result<int>>;

public sealed class SaveTourValidator : AbstractValidator<SaveTourCommand>
{
    public SaveTourValidator()
    {
        RuleFor(x => x.Fields.TourType).Must(t => t is null || TourTypes.All.Contains(t)).WithMessage("Unknown tour type.");
        RuleFor(x => x.Fields.DurationMinutes).InclusiveBetween(10, 240).When(x => x.Fields.DurationMinutes is not null);
        RuleFor(x => x.Fields.Adults).InclusiveBetween(0, 20).When(x => x.Fields.Adults is not null);
        RuleFor(x => x.Fields.Children).InclusiveBetween(0, 20).When(x => x.Fields.Children is not null);
        RuleFor(x => x.Fields.Notes).MaximumLength(1000);
    }
}

public sealed class SetTourStatusValidator : AbstractValidator<SetTourStatusCommand>
{
    public SetTourStatusValidator() => RuleFor(x => x.Status).Must(s => TourStatuses.All.Contains(s) && s != TourStatuses.Scheduled).WithMessage("Choose completed, cancelled or no-show.");
}

public sealed class AddBlockOffValidator : AbstractValidator<AddBlockOffCommand>
{
    public AddBlockOffValidator()
    {
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("The end must be after the start.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

/// <summary>Writes the confirmation and reminder e-mails (English and Arabic together, since an enquirer has no language setting yet).</summary>
public sealed class TourMail(IAppDbContext db, CentreCalendar calendar, IClock clock)
{
    public async Task QueueAsync(Tour tour, bool reminder, CancellationToken ct)
    {
        var enquiry = await db.Enquiries.AsNoTracking().FirstAsync(e => e.Id == tour.EnquiryId, ct);
        if (string.IsNullOrWhiteSpace(enquiry.Email))
        {
            return;
        }

        var centre = await db.Tenants.AsNoTracking().Select(t => t.Name).FirstAsync(ct);
        var local = TimeZoneInfo.ConvertTime(tour.StartsAt, calendar.Zone);
        var when = local.ToString("dddd d MMMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var whenAr = local.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string Name(string lang) => centre.TryGetValue(lang, out var v) ? v : centre.GetValueOrDefault("en") ?? string.Empty;
        var subject = reminder
            ? $"Reminder: your visit to {Name("en")} is tomorrow | تذكير: زيارتكم إلى {Name("ar")} غداً"
            : $"Your visit to {Name("en")} is booked | تم حجز زيارتكم إلى {Name("ar")}";
        var body = reminder
            ? $"Dear {enquiry.FamilyName},\n\nA reminder that we are expecting you on {when} to see {Name("en")}. If you need to change the time, please reply or call us.\n\n---\n\nعزيزي {enquiry.FamilyName}،\n\nنذكركم بأننا في انتظاركم بتاريخ {whenAr} لزيارة {Name("ar")}. إذا رغبتم بتغيير الموعد يرجى التواصل معنا."
            : $"Dear {enquiry.FamilyName},\n\nThank you for your interest in {Name("en")}. Your visit is booked for {when}. We look forward to meeting you and {enquiry.ChildName}.\n\n---\n\nعزيزي {enquiry.FamilyName}،\n\nشكراً لاهتمامكم بـ {Name("ar")}. تم حجز زيارتكم بتاريخ {whenAr}. نتطلع للقائكم أنتم و{enquiry.ChildName}.";
        db.OutboundEmails.Add(new OutboundEmail { ToAddress = enquiry.Email!, Subject = subject, Body = body, NextAttemptAt = clock.UtcNow });
    }
}

internal static class TourScope
{
    public static IQueryable<TourDto> Project(IAppDbContext db, IQueryable<Tour> q) =>
        q.Select(t => new TourDto(
            t.Id, t.EnquiryId,
            db.Enquiries.Where(e => e.Id == t.EnquiryId).Select(e => e.FamilyName).First(),
            db.Enquiries.Where(e => e.Id == t.EnquiryId).Select(e => e.ChildName).First(),
            t.StaffId, db.Staff.Where(s => s.Id == t.StaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
            t.StartsAt, t.DurationMinutes, t.TourType, t.Adults, t.Children, t.Notes, t.Status));

    /// <summary>Why this time does not work for this person, or null when it is free.</summary>
    public static async Task<Error?> ConflictAsync(IAppDbContext db, Guid? staffId, DateTimeOffset start, DateTimeOffset end, Guid? ignoreTour, CancellationToken ct)
    {
        if (await db.TourBlockOffs.AnyAsync(b => (b.StaffId == null || b.StaffId == staffId) && b.StartsAt < end && b.EndsAt > start, ct))
        {
            return Error.Conflict("That time is blocked off. Choose another time or person.", "starts_at");
        }

        if (staffId is not null)
        {
            var others = await db.Tours.AsNoTracking()
                .Where(t => t.StaffId == staffId && t.Status == TourStatuses.Scheduled && t.Id != ignoreTour && t.StartsAt < end).ToListAsync(ct);
            if (others.Any(t => t.StartsAt.AddMinutes(t.DurationMinutes) > start))
            {
                return Error.Conflict("That person already has a tour at this time.", "starts_at");
            }
        }

        return null;
    }
}

public sealed class ListToursHandler(IAppDbContext db) : IRequestHandler<ListToursQuery, Result<IReadOnlyList<TourDto>>>
{
    public async Task<Result<IReadOnlyList<TourDto>>> Handle(ListToursQuery request, CancellationToken ct)
    {
        var q = db.Tours.AsNoTracking().AsQueryable();
        if (request.From is { } from) q = q.Where(t => t.StartsAt >= from);
        if (request.To is { } to) q = q.Where(t => t.StartsAt < to);
        if (request.StaffId is { } staff) q = q.Where(t => t.StaffId == staff);
        if (request.Status is not null) q = q.Where(t => t.Status == request.Status);
        return Result<IReadOnlyList<TourDto>>.Success(await TourScope.Project(db, q.OrderBy(t => t.StartsAt).Take(500)).ToListAsync(ct));
    }
}

public sealed class SaveTourHandler(IAppDbContext db, TourMail mail, IClock clock) : IRequestHandler<SaveTourCommand, Result<TourDto>>
{
    public async Task<Result<TourDto>> Handle(SaveTourCommand request, CancellationToken ct)
    {
        var f = request.Fields;
        var enquiry = await db.Enquiries.FirstOrDefaultAsync(e => e.Id == f.EnquiryId, ct);
        if (enquiry is null)
        {
            return Error.Validation("The enquiry does not exist.", "enquiry_id");
        }

        if (f.StaffId is { } sid && !await db.Staff.AnyAsync(s => s.Id == sid, ct))
        {
            return Error.Validation("The staff member does not exist.", "staff_id");
        }

        if (request.Id is null && f.StartsAt < clock.UtcNow.AddMinutes(-5))
        {
            return Error.Validation("The tour time cannot be in the past.", "starts_at");
        }

        var duration = f.DurationMinutes ?? 30;
        if (await TourScope.ConflictAsync(db, f.StaffId, f.StartsAt, f.StartsAt.AddMinutes(duration), request.Id, ct) is { } conflict)
        {
            return conflict;
        }

        Tour tour;
        var rescheduled = false;
        if (request.Id is { } id)
        {
            var found = await db.Tours.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (found is null)
            {
                return Error.NotFound("Tour not found.");
            }

            if (found.Status != TourStatuses.Scheduled)
            {
                return Error.InvalidState("Only a scheduled tour can be changed.");
            }

            rescheduled = found.StartsAt != f.StartsAt;
            tour = found;
        }
        else
        {
            tour = new Tour { EnquiryId = enquiry.Id };
            db.Tours.Add(tour);
            if (enquiry.Stage is not (EnquiryStages.TourAppointment or EnquiryStages.Toured or EnquiryStages.Enrolled))
            {
                db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = enquiry.Id, FromStage = enquiry.Stage, ToStage = EnquiryStages.TourAppointment, Reason = "Tour booked" });
                enquiry.Stage = EnquiryStages.TourAppointment;
            }
        }

        tour.StartsAt = f.StartsAt;
        tour.DurationMinutes = duration;
        tour.StaffId = f.StaffId;
        tour.TourType = f.TourType ?? "general";
        tour.Adults = f.Adults ?? 1;
        tour.Children = f.Children ?? 1;
        tour.Notes = f.Notes?.Trim();
        if (rescheduled)
        {
            tour.ReminderSentAt = null;
        }

        if (request.Id is null || rescheduled)
        {
            await mail.QueueAsync(tour, false, ct);
        }

        await db.SaveChangesAsync(ct);
        return Result<TourDto>.Success(await TourScope.Project(db, db.Tours.AsNoTracking().Where(t => t.Id == tour.Id)).FirstAsync(ct));
    }
}

public sealed class SetTourStatusHandler(IAppDbContext db, CentreCalendar calendar) : IRequestHandler<SetTourStatusCommand, Result<TourDto>>
{
    public async Task<Result<TourDto>> Handle(SetTourStatusCommand request, CancellationToken ct)
    {
        var tour = await db.Tours.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (tour is null)
        {
            return Error.NotFound("Tour not found.");
        }

        if (tour.Status != TourStatuses.Scheduled)
        {
            return Error.InvalidState("This tour is already closed.");
        }

        tour.Status = request.Status;
        if (request.Status == TourStatuses.Completed)
        {
            var enquiry = await db.Enquiries.FirstAsync(e => e.Id == tour.EnquiryId, ct);
            if (enquiry.Stage is not (EnquiryStages.Toured or EnquiryStages.Enrolled))
            {
                db.EnquiryStageLogs.Add(new EnquiryStageLog { EnquiryId = enquiry.Id, FromStage = enquiry.Stage, ToStage = EnquiryStages.Toured, Reason = "Tour completed" });
                enquiry.Stage = EnquiryStages.Toured;
            }

            db.FollowUpTasks.Add(new FollowUpTask
            {
                Title = $"Follow up with {enquiry.FamilyName} after their tour", DueDate = calendar.Today.AddDays(2),
                AssignedStaffId = tour.StaffId ?? enquiry.AssignedStaffId, EnquiryId = enquiry.Id, TourId = tour.Id,
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<TourDto>.Success(await TourScope.Project(db, db.Tours.AsNoTracking().Where(t => t.Id == tour.Id)).FirstAsync(ct));
    }
}

public sealed class ListBlockOffsHandler(IAppDbContext db) : IRequestHandler<ListBlockOffsQuery, Result<IReadOnlyList<BlockOffDto>>>
{
    public async Task<Result<IReadOnlyList<BlockOffDto>>> Handle(ListBlockOffsQuery request, CancellationToken ct)
    {
        var q = db.TourBlockOffs.AsNoTracking().AsQueryable();
        if (request.From is { } from) q = q.Where(b => b.EndsAt > from);
        if (request.To is { } to) q = q.Where(b => b.StartsAt < to);
        return Result<IReadOnlyList<BlockOffDto>>.Success(await q.OrderBy(b => b.StartsAt).Take(500)
            .Select(b => new BlockOffDto(b.Id, b.StaffId, db.Staff.Where(s => s.Id == b.StaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(), b.StartsAt, b.EndsAt, b.Reason))
            .ToListAsync(ct));
    }
}

public sealed class AddBlockOffHandler(IAppDbContext db) : IRequestHandler<AddBlockOffCommand, Result<BlockOffDto>>
{
    public async Task<Result<BlockOffDto>> Handle(AddBlockOffCommand request, CancellationToken ct)
    {
        if (request.StaffId is { } sid && !await db.Staff.AnyAsync(s => s.Id == sid, ct))
        {
            return Error.Validation("The staff member does not exist.", "staff_id");
        }

        var block = new TourBlockOff { StaffId = request.StaffId, StartsAt = request.StartsAt, EndsAt = request.EndsAt, Reason = request.Reason?.Trim() };
        db.TourBlockOffs.Add(block);
        await db.SaveChangesAsync(ct);
        var name = request.StaffId is { } id ? await db.Staff.AsNoTracking().Where(s => s.Id == id).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct) : null;
        return Result<BlockOffDto>.Success(new BlockOffDto(block.Id, block.StaffId, name, block.StartsAt, block.EndsAt, block.Reason));
    }
}

public sealed class DeleteBlockOffHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteBlockOffCommand, Result>
{
    public async Task<Result> Handle(DeleteBlockOffCommand request, CancellationToken ct)
    {
        var block = await db.TourBlockOffs.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
        if (block is null)
        {
            return Result.Failure(Error.NotFound("Block-off not found."));
        }

        block.IsDeleted = true;
        block.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ListTasksHandler(IAppDbContext db) : IRequestHandler<ListTasksQuery, Result<CursorPage<TaskDto>>>
{
    public async Task<Result<CursorPage<TaskDto>>> Handle(ListTasksQuery request, CancellationToken ct)
    {
        var q = db.FollowUpTasks.AsNoTracking().Where(t => t.IsDone == request.Done);
        if (request.StaffId is { } staff) q = q.Where(t => t.AssignedStaffId == staff);
        return Result<CursorPage<TaskDto>>.Success(await q.ToCursorPageAsync(request.Cursor, request.Limit, t => new TaskDto(
            t.Id, t.Title, t.DueDate, t.AssignedStaffId, db.Staff.Where(s => s.Id == t.AssignedStaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
            t.EnquiryId, db.Enquiries.Where(e => e.Id == t.EnquiryId).Select(e => e.FamilyName).FirstOrDefault(), t.IsDone, t.CreatedAt), ct));
    }
}

public sealed class CompleteTaskHandler(IAppDbContext db, IClock clock) : IRequestHandler<CompleteTaskCommand, Result<TaskDto>>
{
    public async Task<Result<TaskDto>> Handle(CompleteTaskCommand request, CancellationToken ct)
    {
        var task = await db.FollowUpTasks.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (task is null)
        {
            return Error.NotFound("Task not found.");
        }

        task.IsDone = true;
        task.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<TaskDto>.Success(new TaskDto(task.Id, task.Title, task.DueDate, task.AssignedStaffId, null, task.EnquiryId, null, true, task.CreatedAt));
    }
}

/// <summary>The daily run: queues a reminder e-mail for every scheduled tour starting within the next 24 hours that has not had one.</summary>
public sealed class RunTourRemindersHandler(IAppDbContext db, TourMail mail, IClock clock) : IRequestHandler<RunTourRemindersCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunTourRemindersCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var soon = now.AddHours(24);
        var due = await db.Tours.Where(t => t.Status == TourStatuses.Scheduled && t.ReminderSentAt == null && t.StartsAt > now && t.StartsAt <= soon).ToListAsync(ct);
        foreach (var tour in due)
        {
            await mail.QueueAsync(tour, true, ct);
            tour.ReminderSentAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(due.Count);
    }
}

public sealed class RunOutboundEmailHandler(IAppDbContext db, IEmailSender sender, IClock clock) : IRequestHandler<RunOutboundEmailCommand, Result<int>>
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    public async Task<Result<int>> Handle(RunOutboundEmailCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.OutboundEmails.Where(e => e.Status == "pending" && e.NextAttemptAt <= now).OrderBy(e => e.NextAttemptAt).Take(50).ToListAsync(ct);
        var sent = 0;
        foreach (var mail in due)
        {
            if (!sender.IsConfigured)
            {
                mail.Status = "skipped";
                mail.Error = "E-mail is not set up";
                continue;
            }

            var result = await sender.SendAsync(mail.ToAddress, mail.Subject, mail.Body, ct);
            mail.Attempts++;
            if (result.IsSuccess)
            {
                mail.Status = "sent";
                mail.SentAt = now;
                sent++;
            }
            else if (mail.Attempts > Backoff.Length)
            {
                mail.Status = "failed";
                mail.Error = result.Errors.FirstOrDefault()?.Message;
            }
            else
            {
                mail.NextAttemptAt = now + Backoff[mail.Attempts - 1];
                mail.Error = result.Errors.FirstOrDefault()?.Message;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Success(sent);
    }
}
