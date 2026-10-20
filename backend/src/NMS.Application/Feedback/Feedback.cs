using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Common;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Feedback;

public static class FeedbackKinds
{
    public static readonly IReadOnlyList<string> All = ["problem", "idea", "praise"];
}

/// <summary>A note sent from the in-app feedback button during the pilot.</summary>
public class FeedbackNote : TenantEntity
{
    public Guid UserId { get; set; }

    public string Kind { get; set; } = "idea";

    public string Message { get; set; } = string.Empty;

    /// <summary>The screen the person was on, so the team can reproduce it.</summary>
    public string? Page { get; set; }

    public int? Rating { get; set; }

    public string Status { get; set; } = "new";
}

public sealed record FeedbackDto(Guid Id, Guid UserId, string? UserName, string Kind, string Message, string? Page, int? Rating, string Status, DateTimeOffset CreatedAt) : ICursorItem;

public sealed record SendFeedbackCommand(string Kind, string Message, string? Page, int? Rating) : IRequest<Result<FeedbackDto>>;

public sealed record ListFeedbackQuery(string? Status, string? Cursor, int? Limit) : IRequest<Result<CursorPage<FeedbackDto>>>;

public sealed record SetFeedbackStatusCommand(Guid Id, string Status) : IRequest<Result<FeedbackDto>>;

public sealed class SendFeedbackValidator : AbstractValidator<SendFeedbackCommand>
{
    public SendFeedbackValidator()
    {
        RuleFor(c => c.Kind).Must(k => FeedbackKinds.All.Contains(k)).WithMessage("Choose problem, idea or praise.");
        RuleFor(c => c.Message).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.Page!).MaximumLength(200).When(c => c.Page is not null);
        RuleFor(c => c.Rating!.Value).InclusiveBetween(1, 5).When(c => c.Rating is not null);
    }
}

public sealed class SetFeedbackStatusValidator : AbstractValidator<SetFeedbackStatusCommand>
{
    public SetFeedbackStatusValidator() =>
        RuleFor(c => c.Status).Must(s => s is "new" or "seen" or "done").WithMessage("Status must be new, seen or done.");
}

public sealed class SendFeedbackHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<SendFeedbackCommand, Result<FeedbackDto>>
{
    public async Task<Result<FeedbackDto>> Handle(SendFeedbackCommand request, CancellationToken ct)
    {
        var note = new FeedbackNote
        {
            UserId = current.UserId, Kind = request.Kind, Message = request.Message.Trim(), Page = request.Page?.Trim(), Rating = request.Rating,
        };
        db.FeedbackNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return Result<FeedbackDto>.Success(new FeedbackDto(note.Id, note.UserId, null, note.Kind, note.Message, note.Page, note.Rating, note.Status, note.CreatedAt));
    }
}

public sealed class ListFeedbackHandler(IAppDbContext db) : IRequestHandler<ListFeedbackQuery, Result<CursorPage<FeedbackDto>>>
{
    public async Task<Result<CursorPage<FeedbackDto>>> Handle(ListFeedbackQuery request, CancellationToken ct)
    {
        var notes = db.FeedbackNotes.AsNoTracking();
        if (request.Status is { } status)
        {
            notes = notes.Where(n => n.Status == status);
        }

        return Result<CursorPage<FeedbackDto>>.Success(await notes.ToCursorPageAsync(request.Cursor, request.Limit,
            n => new FeedbackDto(n.Id, n.UserId, db.AppUsers.Where(u => u.Id == n.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                n.Kind, n.Message, n.Page, n.Rating, n.Status, n.CreatedAt), ct));
    }
}

public sealed class SetFeedbackStatusHandler(IAppDbContext db) : IRequestHandler<SetFeedbackStatusCommand, Result<FeedbackDto>>
{
    public async Task<Result<FeedbackDto>> Handle(SetFeedbackStatusCommand request, CancellationToken ct)
    {
        var note = await db.FeedbackNotes.FirstOrDefaultAsync(n => n.Id == request.Id, ct);
        if (note is null)
        {
            return Error.NotFound("Feedback not found.");
        }

        note.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return Result<FeedbackDto>.Success(new FeedbackDto(note.Id, note.UserId, null, note.Kind, note.Message, note.Page, note.Rating, note.Status, note.CreatedAt));
    }
}
