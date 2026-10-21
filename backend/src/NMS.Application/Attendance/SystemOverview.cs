using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Application.Learning;
using NMS.Application.Security;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Domain.Communication;
using NMS.Domain.Forms;
using NMS.Domain.Learning;
using NMS.Shared.Results;

namespace NMS.Application.Attendance;

public sealed record PeopleOverview(int Students, int Staff, int Rooms, int PresentToday, int RoomsShort);

public sealed record BillingOverview(long BilledFils, long CollectedFils, long OverdueFils, int OverdueInvoices, long ExpensesFils);

public sealed record AdmissionsOverview(int OpenEnquiries, int NewLast30Days, int ToursNext7Days, int StaleOver7Days, int AdmissionStepsOverdue);

public sealed record LearningOverview(
    int Curricula, int Lessons, int ObservationsLast30Days, int ConcernsLast30Days, int AssessmentsToApprove, int AssessmentsInProgress, int ReportsShared, int ChildrenWithoutObservation30Days);

public sealed record FamilyLearningOverview(int Children, int SharedObservations, int ReportsReady);

public sealed record CommunicationOverview(int PostsToApprove, int NewslettersLast30Days, int LiveForms, int Documents);

/// <summary>One collective picture of the whole centre. Each block appears only for people whose role may see that part of the system.</summary>
public sealed record SystemOverviewDto(
    DateOnly Date, PeopleOverview? People, BillingOverview? Billing, AdmissionsOverview? Admissions, LearningOverview? Learning, FamilyLearningOverview? Family, CommunicationOverview? Communication);

public sealed record SystemOverviewQuery : IRequest<Result<SystemOverviewDto>>, IShortCachedQuery
{
    public string CacheKey => "system";
}

public sealed class SystemOverviewHandler(IAppDbContext db, IAccessScope scope, ICurrentUser current, CentreCalendar calendar, IClock clock) : IRequestHandler<SystemOverviewQuery, Result<SystemOverviewDto>>
{
    public async Task<Result<SystemOverviewDto>> Handle(SystemOverviewQuery request, CancellationToken ct)
    {
        var today = calendar.Today;
        var own = await scope.GetOwnStudentIdsAsync(ct);
        var family = own is not null;
        var thirty = clock.UtcNow.AddDays(-30);
        var from30 = today.AddDays(-30);

        PeopleOverview? people = null;
        if (!family && current.HasPermission(Permissions.StudentsRead))
        {
            var students = await db.Students.AsNoTracking().CountAsync(s => s.Status == StudentStatuses.Active, ct);
            var staff = current.HasPermission(Permissions.StaffRead) ? await db.Staff.AsNoTracking().CountAsync(s => s.Status == "active", ct) : 0;
            var rooms = await db.Rooms.AsNoTracking().CountAsync(r => r.IsActive, ct);
            var present = current.HasPermission(Permissions.AttendanceRead)
                ? await db.AttendanceRecords.AsNoTracking().CountAsync(a => a.Date == today && a.PersonType == PersonTypes.Student && a.CheckInAt != null, ct)
                : 0;
            var short_ = current.HasPermission(Permissions.AttendanceWrite) ? (await RatioCalculator.ForRoomsAsync(db, today, null, ct)).Count(r => r.IsBreach) : 0;
            people = new PeopleOverview(students, staff, rooms, present, short_);
        }

        BillingOverview? billing = null;
        if (!family && current.HasPermission(Permissions.BillingWrite))
        {
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var live = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled);
            var billed = await live.Where(i => i.IssueDate >= monthStart).SumAsync(i => (long?)i.TotalFils, ct) ?? 0;
            var collected = await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= monthStart).SumAsync(p => (long?)p.AmountFils, ct) ?? 0;
            var overdueRows = await live.Where(i => i.Status == InvoiceStatuses.Overdue).Select(i => i.TotalFils - i.PaidFils).ToListAsync(ct);
            var spent = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= monthStart).SumAsync(e => (long?)e.AmountFils, ct) ?? 0;
            billing = new BillingOverview(billed, collected, overdueRows.Sum(), overdueRows.Count, spent);
        }

        AdmissionsOverview? admissions = null;
        if (!family && current.HasPermission(Permissions.EnquiriesRead))
        {
            var open = db.Enquiries.AsNoTracking().Where(e => !e.IsArchived && e.Stage != EnquiryStages.Enrolled);
            var now = clock.UtcNow;
            var openCount = await open.CountAsync(ct);
            var fresh = await db.Enquiries.AsNoTracking().CountAsync(e => !e.IsArchived && e.EnquiryDate >= from30, ct);
            var tours = current.HasPermission(Permissions.ToursRead) ? await db.Tours.AsNoTracking().CountAsync(t => t.StartsAt >= now && t.StartsAt <= now.AddDays(7), ct) : 0;
            var staleLimit = today.AddDays(-7);
            var stale = await open.CountAsync(e => (e.LastContactDate ?? e.EnquiryDate) < staleLimit, ct);
            var steps = current.HasPermission(Permissions.FormsRead) ? await db.StudentAdmissionSteps.AsNoTracking().CountAsync(s => s.Status == AdmissionStatuses.Current && s.DueDate != null && s.DueDate < today, ct) : 0;
            admissions = new AdmissionsOverview(openCount, fresh, tours, stale, steps);
        }

        LearningOverview? learning = null;
        if (!family && current.HasPermission(Permissions.LearningRead))
        {
            var visible = (await StudentScope.VisibleAsync(db, scope, ct)).Where(s => s.Status == StudentStatuses.Active).Select(s => s.Id);
            var visibleCount = await visible.CountAsync(ct);
            var observed = await db.Observations.AsNoTracking().Where(o => o.ObservedAt >= thirty && visible.Contains(o.StudentId)).Select(o => o.StudentId).Distinct().CountAsync(ct);
            learning = new LearningOverview(
                await db.CurriculumFrameworks.AsNoTracking().CountAsync(ct),
                await db.Lessons.AsNoTracking().CountAsync(ct),
                await db.Observations.AsNoTracking().CountAsync(o => o.ObservedAt >= thirty && visible.Contains(o.StudentId), ct),
                await db.Observations.AsNoTracking().CountAsync(o => o.ObservedAt >= thirty && o.Type == ObservationTypes.Concern && visible.Contains(o.StudentId), ct),
                await db.AssessmentRuns.AsNoTracking().CountAsync(r => r.Status == AssessmentStatuses.Completed && !r.ParentApproved && visible.Contains(r.StudentId), ct),
                await db.AssessmentRuns.AsNoTracking().CountAsync(r => r.Status == AssessmentStatuses.Draft && visible.Contains(r.StudentId), ct),
                await db.AssessmentRuns.AsNoTracking().CountAsync(r => r.ParentApproved && visible.Contains(r.StudentId), ct),
                Math.Max(0, visibleCount - observed));
        }

        FamilyLearningOverview? familyLearning = null;
        if (family && current.HasPermission(Permissions.JourneyRead))
        {
            var kids = own!;
            familyLearning = new FamilyLearningOverview(
                kids.Count,
                await db.Observations.AsNoTracking().CountAsync(o => kids.Contains(o.StudentId) && o.ShareWithParents && o.Type != ObservationTypes.Concern, ct),
                await db.AssessmentRuns.AsNoTracking().CountAsync(r => kids.Contains(r.StudentId) && r.ParentApproved, ct));
        }

        CommunicationOverview? communication = null;
        if (!family && (current.HasPermission(Permissions.ActivitiesRead) || current.HasPermission(Permissions.MessagingRead)))
        {
            communication = new CommunicationOverview(
                current.HasPermission(Permissions.ActivitiesApprove) ? await db.ActivityPosts.AsNoTracking().CountAsync(p => p.ApprovalStatus == ApprovalStatuses.Pending, ct) : 0,
                current.HasPermission(Permissions.MessagingRead) ? await db.Newsletters.AsNoTracking().CountAsync(n => n.Status == NewsletterStatuses.Published && n.PublishedAt >= thirty, ct) : 0,
                current.HasPermission(Permissions.FormsRead) ? await db.Forms.AsNoTracking().CountAsync(f => f.Status == FormStatuses.Live, ct) : 0,
                current.HasPermission(Permissions.ResourcesRead) ? await db.ResourceDocuments.AsNoTracking().CountAsync(ct) : 0);
        }

        return Result<SystemOverviewDto>.Success(new SystemOverviewDto(today, people, billing, admissions, learning, familyLearning, communication));
    }
}
