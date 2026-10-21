using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.Auth;
using NMS.Application.Common;
using NMS.Application.Reporting;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Auditing;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;
using NMS.Shared.Results;

namespace NMS.Application.CentreGroups;

/// <summary>
/// A group of centres is a head centre with branch centres under it (one level only).
/// Every branch keeps its own private data; the group views below read each branch one at a time, as the group owner.
/// </summary>
public sealed class CentreRunner(IServiceScopeFactory scopes)
{
    /// <summary>Runs work as if signed in to one centre, in its own scope so nothing leaks between centres.</summary>
    public async Task<T> RunAsync<T>(Guid centreId, Func<IServiceProvider, Task<T>> work)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = centreId;
        return await work(scope.ServiceProvider);
    }
}

public sealed record GroupCentreDto(Guid Id, string Slug, Dictionary<string, string> Name, bool IsHead, string Status);

internal static class GroupMembers
{
    /// <summary>The head centre of the group the signed-in centre belongs to, and every centre in that group.</summary>
    public static async Task<(Guid HeadId, List<Tenant> Centres)> OfAsync(IAppDbContext db, Guid centreId, CancellationToken ct)
    {
        var me = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == centreId, ct);
        var head = me.ParentTenantId ?? me.Id;
        var centres = await db.Tenants.AsNoTracking()
            .Where(t => !t.IsDeleted && (t.Id == head || t.ParentTenantId == head))
            .OrderBy(t => t.CreatedAt).ToListAsync(ct);
        return (head, centres);
    }
}

// ---------------------------------------------------------------- the centres of my group

public sealed record MyCentresQuery : IRequest<Result<List<GroupCentreDto>>>;

/// <summary>The centres in this group where the signed-in person already has an account: the list behind "switch centre".</summary>
public sealed class MyCentresHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current) : IRequestHandler<MyCentresQuery, Result<List<GroupCentreDto>>>
{
    public async Task<Result<List<GroupCentreDto>>> Handle(MyCentresQuery request, CancellationToken ct)
    {
        var (head, centres) = await GroupMembers.OfAsync(db, tenant.TenantId, ct);
        var email = (current.Email ?? string.Empty).ToLowerInvariant();
        var mine = await db.LoginDirectory.AsNoTracking().Where(d => d.Email == email).Select(d => d.CentreId).ToListAsync(ct);
        return Result<List<GroupCentreDto>>.Success(centres
            .Where(c => mine.Contains(c.Id) && c.Status == "active")
            .Select(c => new GroupCentreDto(c.Id, c.Slug, c.Name, c.Id == head, c.Status)).ToList());
    }
}

// ---------------------------------------------------------------- one sign-in for every centre

public sealed record SwitchCentreCommand(Guid CentreId, string? IpAddress) : IRequest<Result<AuthSession>>;

public sealed class SwitchCentreHandler(
    IAppDbContext db, ITenantContext tenant, ICurrentUser current, ITokenService tokens, IClock clock, IOptions<AuthOptions> options)
    : IRequestHandler<SwitchCentreCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(SwitchCentreCommand request, CancellationToken ct)
    {
        var (_, centres) = await GroupMembers.OfAsync(db, tenant.TenantId, ct);
        var target = centres.FirstOrDefault(c => c.Id == request.CentreId && c.Status == "active");
        if (target is null)
        {
            return Error.NotFound();
        }

        var email = (current.Email ?? string.Empty).ToLowerInvariant();
        tenant.TenantId = target.Id;
        var user = await db.AppUsers.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || user.Status != "active")
        {
            return Error.Forbidden("You do not have an account at that centre.");
        }

        var session = await new SessionFactory(db, tokens, clock, options).IssueAsync(user, Guid.NewGuid(), request.IpAddress, ct);
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }
}

// ---------------------------------------------------------------- create a new centre in the group

public sealed record ProvisionCentreCommand(string Name, string? NameArabic, string Slug) : IRequest<Result<GroupCentreDto>>;

public sealed class ProvisionCentreValidator : AbstractValidator<ProvisionCentreCommand>
{
    public ProvisionCentreValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.NameArabic).MaximumLength(150);
        RuleFor(c => c.Slug).Matches("^[a-z0-9][a-z0-9-]{2,38}$").WithMessage("The web name must be 3 to 39 lowercase letters, numbers or dashes.");
    }
}

public sealed class ProvisionCentreHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, CentreRunner runner, IClock clock)
    : IRequestHandler<ProvisionCentreCommand, Result<GroupCentreDto>>
{
    public async Task<Result<GroupCentreDto>> Handle(ProvisionCentreCommand request, CancellationToken ct)
    {
        var head = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.TenantId, ct);
        if (head.ParentTenantId is not null)
        {
            return Error.Forbidden("Only the head centre of a group can add centres.");
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            return Error.Conflict("That web name is already used.", "slug");
        }

        var owner = await db.AppUsers.AsNoTracking().FirstAsync(u => u.Id == current.UserId, ct);
        var now = clock.UtcNow;
        var name = new Dictionary<string, string> { ["en"] = request.Name.Trim() };
        if (!string.IsNullOrWhiteSpace(request.NameArabic))
        {
            name["ar"] = request.NameArabic.Trim();
        }

        var centre = new Tenant
        {
            Name = name, Slug = slug, DeploymentMode = head.DeploymentMode, ParentTenantId = head.Id,
            ContactEmail = head.ContactEmail, ContactPhone = head.ContactPhone, WeekendDays = head.WeekendDays, Timezone = head.Timezone,
            CreatedAt = now, UpdatedAt = now, CreatedBy = owner.Id, UpdatedBy = owner.Id,
        };
        db.Tenants.Add(centre);
        await db.SaveChangesAsync(ct);

        // The group owner gets the same sign-in at the new centre, so one password opens every centre.
        await runner.RunAsync(centre.Id, async sp =>
        {
            var branch = sp.GetRequiredService<IAppDbContext>();
            var roles = SystemRoleFactory.Create(owner.Id).ToDictionary(r => r.Name);
            branch.Roles.AddRange(roles.Values);
            var admin = new AppUser
            {
                Email = owner.Email, EmailVerified = true, FirstName = owner.FirstName, LastName = owner.LastName,
                PreferredLanguage = owner.PreferredLanguage, PasswordHash = owner.PasswordHash, CreatedBy = owner.Id, UpdatedBy = owner.Id,
            };
            admin.Roles.Add(new AppUserRole { Role = roles[SystemRoles.SuperAdmin], AssignedAt = now, AssignedBy = owner.Id });
            branch.AppUsers.Add(admin);
            branch.LoginDirectory.Add(new LoginDirectoryEntry { Email = admin.Email, CentreId = centre.Id, UserId = admin.Id });
            await branch.SaveChangesAsync(ct);
            return true;
        });

        return Result<GroupCentreDto>.Success(new GroupCentreDto(centre.Id, centre.Slug, centre.Name, false, centre.Status));
    }
}

// ---------------------------------------------------------------- the same person at more than one centre

public sealed record AssignStaffToCentreCommand(string Email, Guid CentreId, string Role) : IRequest<Result>;

public sealed class AssignStaffToCentreValidator : AbstractValidator<AssignStaffToCentreCommand>
{
    public AssignStaffToCentreValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Role).Must(r => SystemRoles.All.Contains(r) && r != SystemRoles.Parent).WithMessage("Choose a staff role.");
    }
}

public sealed class AssignStaffToCentreHandler(IAppDbContext db, ITenantContext tenant, CentreRunner runner, IClock clock)
    : IRequestHandler<AssignStaffToCentreCommand, Result>
{
    public async Task<Result> Handle(AssignStaffToCentreCommand request, CancellationToken ct)
    {
        var (_, centres) = await GroupMembers.OfAsync(db, tenant.TenantId, ct);
        if (request.CentreId == tenant.TenantId || centres.All(c => c.Id != request.CentreId))
        {
            return Result.Failure(Error.NotFound());
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return Result.Failure(Error.NotFound());
        }

        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.AppUserId == user.Id, ct);
        var now = clock.UtcNow;
        return await runner.RunAsync(request.CentreId, async sp =>
        {
            var branch = sp.GetRequiredService<IAppDbContext>();
            if (await branch.AppUsers.AnyAsync(u => u.Email == email, ct))
            {
                return Result.Failure(Error.Conflict("This person already has an account at that centre."));
            }

            var role = await branch.Roles.FirstOrDefaultAsync(r => r.Name == request.Role, ct);
            if (role is null)
            {
                return Result.Failure(Error.NotFound());
            }

            var copy = new AppUser
            {
                Email = user.Email, EmailVerified = user.EmailVerified, FirstName = user.FirstName, LastName = user.LastName,
                PreferredLanguage = user.PreferredLanguage, PasswordHash = user.PasswordHash, CreatedBy = user.Id, UpdatedBy = user.Id,
            };
            copy.Roles.Add(new AppUserRole { RoleId = role.Id, AssignedAt = now, AssignedBy = user.Id });
            branch.AppUsers.Add(copy);
            branch.LoginDirectory.Add(new LoginDirectoryEntry { Email = copy.Email, CentreId = request.CentreId, UserId = copy.Id });

            if (staff is not null)
            {
                branch.Staff.Add(new Staff
                {
                    AppUserId = copy.Id, FirstName = staff.FirstName, LastName = staff.LastName, DisplayName = staff.DisplayName,
                    Email = staff.Email, Phone = staff.Phone, JobTitle = staff.JobTitle, Gender = staff.Gender, HireDate = staff.HireDate,
                    Status = "active", Barcode = BarcodeGenerator.Next("STF"),
                });
            }

            await branch.SaveChangesAsync(ct);
            return Result.Success();
        });
    }
}

// ---------------------------------------------------------------- moving a child to another centre

public sealed record TransferStudentCommand(Guid StudentId, Guid ToCentreId, string? Reason) : IRequest<Result<Guid>>;

public sealed class TransferStudentValidator : AbstractValidator<TransferStudentCommand>
{
    public TransferStudentValidator() => RuleFor(c => c.Reason).MaximumLength(500);
}

public sealed class TransferStudentHandler(IAppDbContext db, ITenantContext tenant, ICurrentUser current, CentreRunner runner, IClock clock)
    : IRequestHandler<TransferStudentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(TransferStudentCommand request, CancellationToken ct)
    {
        var from = tenant.TenantId;
        var (_, centres) = await GroupMembers.OfAsync(db, from, ct);
        if (request.ToCentreId == from || centres.All(c => c.Id != request.ToCentreId))
        {
            return Error.NotFound();
        }

        var student = await db.Students.Include(s => s.StudentGuardians).ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, ct);
        if (student is null)
        {
            return Error.NotFound();
        }

        if (student.Status != StudentStatuses.Active)
        {
            return Error.InvalidState("Only a child who is currently enrolled can be moved to another centre.");
        }

        var now = clock.UtcNow;
        var newId = await runner.RunAsync(request.ToCentreId, async sp =>
        {
            var branch = sp.GetRequiredService<IAppDbContext>();
            var copy = new Student
            {
                FirstName = student.FirstName, LastName = student.LastName, DisplayName = student.DisplayName,
                FirstNameArabic = student.FirstNameArabic, LastNameArabic = student.LastNameArabic,
                DateOfBirth = student.DateOfBirth, Gender = student.Gender, Nationality = student.Nationality,
                Religion = student.Religion, PrimaryLanguage = student.PrimaryLanguage,
                MedicalNotes = student.MedicalNotes, AllergyInfo = student.AllergyInfo, DietaryRequirements = student.DietaryRequirements,
                CustodyNotes = student.CustodyNotes, PassportNumber = student.PassportNumber, EmiratesId = student.EmiratesId,
                Status = StudentStatuses.Active, StatusChangedAt = now, StartDate = DateOnly.FromDateTime(now.UtcDateTime),
                Barcode = BarcodeGenerator.Next("STU"),
            };
            branch.Students.Add(copy);
            foreach (var link in student.StudentGuardians)
            {
                var g = link.Guardian!;
                var email = g.Email.Trim().ToLowerInvariant();
                var guardian = await branch.Guardians.FirstOrDefaultAsync(x => x.Email == email, ct);
                if (guardian is null)
                {
                    guardian = new Guardian
                    {
                        FirstName = g.FirstName, LastName = g.LastName, DisplayName = g.DisplayName, Email = email, Phone = g.Phone,
                        Relationship = g.Relationship, IsPrimaryContact = g.IsPrimaryContact, CanPickup = g.CanPickup,
                        ReceivesCommunications = g.ReceivesCommunications, Nationality = g.Nationality, EmiratesId = g.EmiratesId,
                        Occupation = g.Occupation, Employer = g.Employer,
                    };
                    branch.Guardians.Add(guardian);
                }

                copy.StudentGuardians.Add(new StudentGuardian { Student = copy, Guardian = guardian, IsPrimary = link.IsPrimary });
            }

            branch.AuditLogs.Add(Entry(request.ToCentreId, copy.Id, "from_centre", from));
            await branch.SaveChangesAsync(ct);
            return copy.Id;
        });

        var moved = student.TransitionTo(StudentStatuses.Transferred, request.Reason ?? "Moved to another centre in the group", now);
        if (moved.IsFailure)
        {
            return Result<Guid>.Failure(moved.Errors);
        }

        db.AuditLogs.Add(Entry(from, student.Id, "to_centre", request.ToCentreId));
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(newId);

        AuditLog Entry(Guid centre, Guid entityId, string action, Guid otherCentre) => new()
        {
            TenantId = centre, UserId = current.UserId, UserEmail = current.Email, EntityType = "student", EntityId = entityId, Action = AuditActions.Update,
            ChangedFields = new Dictionary<string, AuditFieldChange> { [action] = new(null, otherCentre) },
            IpAddress = current.IpAddress, UserAgent = current.UserAgent, RequestId = current.RequestId, OccurredAt = now,
        };
    }
}

// ---------------------------------------------------------------- group dashboard and reports

public sealed record CentreFigures(
    Guid CentreId, string Slug, Dictionary<string, string> Name, bool IsHead,
    int Students, int Staff, int PresentToday, int JoinedThisMonth, int LeftThisMonth, int AttendanceDays, int OpenDays,
    long BilledFils, long CollectedFils, long OverdueFils, long ExpensesFils, int OpenEnquiries);

public sealed record GroupOverviewDto(DateOnly Date, IReadOnlyList<CentreFigures> Centres, CentreFigures Total);

internal static class GroupFigures
{
    /// <summary>Every figure for one centre; runs inside that centre's own scope.</summary>
    public static async Task<CentreFigures> CollectAsync(IServiceProvider sp, Tenant centre, bool isHead, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IAppDbContext>();
        var today = sp.GetRequiredService<CentreCalendar>().Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var records = db.AttendanceRecords.AsNoTracking().Where(a => a.PersonType == PersonTypes.Student && a.CheckInAt != null);
        var live = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled);
        var overdue = await live.Where(i => i.Status == InvoiceStatuses.Overdue).Select(i => i.TotalFils - i.PaidFils).ToListAsync(ct);
        return new CentreFigures(
            centre.Id, centre.Slug, centre.Name, isHead,
            await db.Students.AsNoTracking().CountAsync(s => s.Status == StudentStatuses.Active, ct),
            await db.Staff.AsNoTracking().CountAsync(s => s.Status == "active", ct),
            await records.CountAsync(a => a.Date == today, ct),
            await db.Students.AsNoTracking().CountAsync(s => s.StartDate >= monthStart, ct),
            await db.Students.AsNoTracking().CountAsync(s => s.EndDate >= monthStart && (s.Status == StudentStatuses.Withdrawn || s.Status == StudentStatuses.Transferred), ct),
            await records.CountAsync(a => a.Date >= monthStart, ct),
            await records.Where(a => a.Date >= monthStart).Select(a => a.Date).Distinct().CountAsync(ct),
            await live.Where(i => i.IssueDate >= monthStart).SumAsync(i => (long?)i.TotalFils, ct) ?? 0,
            await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= monthStart).SumAsync(p => (long?)p.AmountFils, ct) ?? 0,
            overdue.Sum(),
            await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= monthStart).SumAsync(e => (long?)e.AmountFils, ct) ?? 0,
            await db.Enquiries.AsNoTracking().CountAsync(e => !e.IsArchived && e.Stage != EnquiryStages.Enrolled, ct));
    }

    public static async Task<List<CentreFigures>> ForGroupAsync(IAppDbContext db, ITenantContext tenant, CentreRunner runner, CancellationToken ct)
    {
        var (head, centres) = await GroupMembers.OfAsync(db, tenant.TenantId, ct);
        var rows = new List<CentreFigures>();
        foreach (var centre in centres.Where(c => c.Status == "active"))
        {
            rows.Add(await runner.RunAsync(centre.Id, sp => CollectAsync(sp, centre, centre.Id == head, ct)));
        }

        return rows;
    }

    public static CentreFigures Sum(IReadOnlyList<CentreFigures> rows) => new(
        Guid.Empty, "total", new Dictionary<string, string> { ["en"] = "All centres", ["ar"] = "جميع الفروع" }, false,
        rows.Sum(r => r.Students), rows.Sum(r => r.Staff), rows.Sum(r => r.PresentToday), rows.Sum(r => r.JoinedThisMonth), rows.Sum(r => r.LeftThisMonth),
        rows.Sum(r => r.AttendanceDays), rows.Count == 0 ? 0 : rows.Max(r => r.OpenDays),
        rows.Sum(r => r.BilledFils), rows.Sum(r => r.CollectedFils), rows.Sum(r => r.OverdueFils), rows.Sum(r => r.ExpensesFils), rows.Sum(r => r.OpenEnquiries));
}

public sealed record GroupOverviewQuery : IRequest<Result<GroupOverviewDto>>;

public sealed class GroupOverviewHandler(IAppDbContext db, ITenantContext tenant, CentreRunner runner, CentreCalendar calendar)
    : IRequestHandler<GroupOverviewQuery, Result<GroupOverviewDto>>
{
    public async Task<Result<GroupOverviewDto>> Handle(GroupOverviewQuery request, CancellationToken ct)
    {
        var rows = await GroupFigures.ForGroupAsync(db, tenant, runner, ct);
        return Result<GroupOverviewDto>.Success(new GroupOverviewDto(calendar.Today, rows, GroupFigures.Sum(rows)));
    }
}

public static class GroupReportKinds
{
    public const string Attendance = "attendance";
    public const string Revenue = "revenue";
    public const string Enrolment = "enrolment";

    public static readonly IReadOnlyList<string> All = [Attendance, Revenue, Enrolment];
}

public sealed record GroupReportQuery(string Kind, string Language) : IRequest<Result<ReportTable>>;

public sealed class GroupReportValidator : AbstractValidator<GroupReportQuery>
{
    public GroupReportValidator() => RuleFor(q => q.Kind).Must(GroupReportKinds.All.Contains).WithMessage("Unknown report.");
}

public sealed class GroupReportHandler(IAppDbContext db, ITenantContext tenant, CentreRunner runner, CentreCalendar calendar)
    : IRequestHandler<GroupReportQuery, Result<ReportTable>>
{
    public async Task<Result<ReportTable>> Handle(GroupReportQuery request, CancellationToken ct)
    {
        var rows = await GroupFigures.ForGroupAsync(db, tenant, runner, ct);
        var all = GroupFigures.Sum(rows);
        var ar = request.Language == "ar";
        string Label(CentreFigures r) => r.Name.TryGetValue(request.Language, out var n) ? n : r.Name.GetValueOrDefault("en") ?? r.Slug;
        static string Aed(long fils) => (fils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        static string Pct(int part, int whole) => whole == 0 ? "0%" : $"{Math.Round(100.0 * part / whole)}%";
        var month = calendar.Today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var centre = ar ? "الفرع" : "Centre";

        IReadOnlyList<string> columns;
        Func<CentreFigures, IReadOnlyList<object?>> row;
        string title;
        switch (request.Kind)
        {
            case GroupReportKinds.Attendance:
                title = ar ? "الحضور الموحّد" : "Consolidated attendance";
                columns = [centre, ar ? "الأطفال" : "Children", ar ? "حاضرون اليوم" : "Here today", ar ? "أيام الحضور (الشهر)" : "Attendance days (month)", ar ? "متوسط يومي" : "Daily average", ar ? "نسبة الحضور" : "Attendance rate"];
                row = r => [Label(r), r.Students, r.PresentToday, r.AttendanceDays, r.OpenDays == 0 ? 0 : Math.Round((double)r.AttendanceDays / r.OpenDays, 1), Pct(r.AttendanceDays, r.Students * Math.Max(1, r.OpenDays))];
                break;
            case GroupReportKinds.Revenue:
                title = ar ? "الإيرادات الموحّدة" : "Consolidated revenue";
                columns = [centre, ar ? "المفوتر (د.إ)" : "Billed (AED)", ar ? "المحصّل (د.إ)" : "Collected (AED)", ar ? "المتأخر (د.إ)" : "Overdue (AED)", ar ? "المصروفات (د.إ)" : "Expenses (AED)", ar ? "الصافي (د.إ)" : "Net collected (AED)"];
                row = r => [Label(r), Aed(r.BilledFils), Aed(r.CollectedFils), Aed(r.OverdueFils), Aed(r.ExpensesFils), Aed(r.CollectedFils - r.ExpensesFils)];
                break;
            default:
                title = ar ? "التسجيل الموحّد" : "Consolidated enrolment";
                columns = [centre, ar ? "الأطفال" : "Children", ar ? "انضموا هذا الشهر" : "Joined this month", ar ? "غادروا هذا الشهر" : "Left this month", ar ? "استفسارات مفتوحة" : "Open enquiries"];
                row = r => [Label(r), r.Students, r.JoinedThisMonth, r.LeftThisMonth, r.OpenEnquiries];
                break;
        }

        var table = rows.Select(row).Append(row(all with { Name = all.Name })).ToList();
        // The last line is the total across every centre; it reads in the requested language.
        table[^1] = row(all with { Name = new Dictionary<string, string> { [request.Language] = ar ? "الإجمالي" : "Total" } });
        return Result<ReportTable>.Success(new ReportTable(title, month, request.Language, columns, table, [(ar ? "عدد الفروع" : "Centres", rows.Count.ToString())]));
    }
}
