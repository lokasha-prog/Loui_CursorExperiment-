using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Domain.Auditing;
using NMS.Domain.CentreManagement;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Reporting;

// ── Audit log viewer ────────────────────────────────────────────────────────

public sealed record AuditLogDto(
    Guid Id, Guid? UserId, string? UserEmail, string EntityType, Guid EntityId, string Action,
    Dictionary<string, AuditFieldChange>? ChangedFields, string? IpAddress, DateTimeOffset OccurredAt) : ICursorItem
{
    public DateTimeOffset CreatedAt => OccurredAt;
}

public sealed record ListAuditLogsQuery(
    Guid? UserId, string? EntityType, string? Action, DateOnly? From, DateOnly? To, string? Cursor, int? Limit)
    : IRequest<Result<CursorPage<AuditLogDto>>>;

public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(q => q.Action!).MaximumLength(30).When(q => q.Action is not null);
        RuleFor(q => q.EntityType!).MaximumLength(60).When(q => q.EntityType is not null);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null)
            .WithMessage("The end date must not be before the start date.");
    }
}

/// <summary>Read-only view of the append-only change history. Sensitive values were already masked when the history was written.</summary>
public sealed class ListAuditLogsHandler(IAppDbContext db) : IRequestHandler<ListAuditLogsQuery, Result<CursorPage<AuditLogDto>>>
{
    public async Task<Result<CursorPage<AuditLogDto>>> Handle(ListAuditLogsQuery request, CancellationToken ct)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (request.UserId is { } user) logs = logs.Where(l => l.UserId == user);
        if (!string.IsNullOrWhiteSpace(request.EntityType)) logs = logs.Where(l => l.EntityType == request.EntityType);
        if (!string.IsNullOrWhiteSpace(request.Action)) logs = logs.Where(l => l.Action == request.Action);
        if (request.From is { } from) { var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); logs = logs.Where(l => l.OccurredAt >= start); }
        if (request.To is { } to) { var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); logs = logs.Where(l => l.OccurredAt < end); }

        var size = CursorToken.NormalizeLimit(request.Limit);
        if (CursorToken.TryDecode(request.Cursor) is { } token)
        {
            var at = token.CreatedAt;
            var id = token.Id;
            logs = logs.Where(l => l.OccurredAt < at || (l.OccurredAt == at && l.Id.CompareTo(id) < 0));
        }

        var rows = await logs.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id).Take(size + 1)
            .Select(l => new AuditLogDto(l.Id, l.UserId, l.UserEmail, l.EntityType, l.EntityId, l.Action, l.ChangedFields, l.IpAddress, l.OccurredAt))
            .ToListAsync(ct);
        var more = rows.Count > size;
        if (more) rows.RemoveAt(rows.Count - 1);
        var next = more && rows.Count > 0 ? new CursorToken(rows[^1].OccurredAt, rows[^1].Id).Encode() : null;
        return Result<CursorPage<AuditLogDto>>.Success(new CursorPage<AuditLogDto>(rows, next, more, size));
    }
}

// ── Emergency report ────────────────────────────────────────────────────────

public sealed record EmergencyContact(string Name, string Relationship, string? Phone, string Email, bool CanPickup, bool IsPrimary);

public sealed record EmergencyChild(
    string Name, string? ArabicName, DateOnly DateOfBirth, string? Gender, string? Room, string? Allergies, string? Medical,
    string? Dietary, string? PrimaryLanguage, IReadOnlyList<EmergencyContact> Contacts);

public sealed record EmergencyReportData(string CentreName, string Title, DateTimeOffset GeneratedAt, string Language, IReadOnlyList<EmergencyChild> Children);

/// <summary>Draws the one-page-per-child emergency sheet. Implemented outside this layer so no PDF library leaks in here.</summary>
public interface IEmergencyReportRenderer
{
    byte[] Render(EmergencyReportData data);
}

public sealed record EmergencyReportQuery(Guid? StudentId, Guid? RoomId, string Language) : IRequest<Result<EmergencyReportFile>>;

public sealed record EmergencyReportFile(byte[] Pdf, string FileName);

public sealed class EmergencyReportValidator : AbstractValidator<EmergencyReportQuery>
{
    public EmergencyReportValidator()
    {
        RuleFor(q => q.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
        RuleFor(q => q).Must(q => (q.StudentId is null) != (q.RoomId is null)).WithMessage("Choose one child or one room.");
    }
}

public sealed class EmergencyReportHandler(
    IAppDbContext db, IAccessScope scope, ICurrentUser current, ITenantContext tenant, IClock clock, IEmergencyReportRenderer renderer)
    : IRequestHandler<EmergencyReportQuery, Result<EmergencyReportFile>>
{
    public async Task<Result<EmergencyReportFile>> Handle(EmergencyReportQuery request, CancellationToken ct)
    {
        var students = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active || s.Status == StudentStatuses.OnHold);
        var rooms = await scope.GetVisibleRoomIdsAsync(ct);
        if (rooms is not null)
        {
            students = students.Where(s => s.RoomId != null && rooms.Contains(s.RoomId.Value));
        }

        students = request.StudentId is { } sid ? students.Where(s => s.Id == sid) : students.Where(s => s.RoomId == request.RoomId);
        var list = await students.Include(s => s.StudentGuardians).ThenInclude(sg => sg.Guardian).OrderBy(s => s.FirstName).ToListAsync(ct);
        if (list.Count == 0)
        {
            return Error.NotFound("Nothing to report on.");
        }

        var roomNames = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var centre = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Name).FirstAsync(ct);
        string Pick(Dictionary<string, string>? d) => d is null ? "" : d.TryGetValue(request.Language, out var v) ? v : d.GetValueOrDefault("en") ?? d.Values.FirstOrDefault() ?? "";

        var children = list.Select(s => new EmergencyChild(
            $"{s.FirstName} {s.LastName}",
            string.IsNullOrWhiteSpace(s.FirstNameArabic) ? null : $"{s.FirstNameArabic} {s.LastNameArabic}",
            s.DateOfBirth, s.Gender, s.RoomId is { } r ? Pick(roomNames.GetValueOrDefault(r)) : null,
            s.AllergyInfo, s.MedicalNotes, s.DietaryRequirements, s.PrimaryLanguage,
            s.StudentGuardians.Where(sg => sg.Guardian is not null)
                .OrderByDescending(sg => sg.Guardian!.IsPrimaryContact)
                .Select(sg => new EmergencyContact(
                    $"{sg.Guardian!.FirstName} {sg.Guardian.LastName}", sg.Guardian.Relationship, sg.Guardian.Phone, sg.Guardian.Email,
                    sg.Guardian.CanPickup, sg.Guardian.IsPrimaryContact)).ToList())).ToList();

        var title = request.RoomId is not null && request.Language == "ar" ? "تقرير الطوارئ للغرفة" : request.RoomId is not null ? "Room emergency report"
            : request.Language == "ar" ? "تقرير الطوارئ للطفل" : "Child emergency report";
        var pdf = renderer.Render(new EmergencyReportData(Pick(centre), title, clock.UtcNow, request.Language, children));

        // Printing medical details is itself recorded in the change history.
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.TenantId, UserId = current.UserId, UserEmail = current.Email, EntityType = request.RoomId is not null ? "room" : "student",
            EntityId = request.RoomId ?? request.StudentId!.Value, Action = AuditActions.Export, IpAddress = current.IpAddress,
            UserAgent = current.UserAgent, RequestId = current.RequestId, OccurredAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result<EmergencyReportFile>.Success(new EmergencyReportFile(pdf, $"emergency-{(request.RoomId is not null ? "room" : "child")}-{clock.UtcNow:yyyyMMdd-HHmm}.pdf"));
    }
}
