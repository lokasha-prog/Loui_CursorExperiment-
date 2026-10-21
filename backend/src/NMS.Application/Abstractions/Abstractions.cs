using Microsoft.EntityFrameworkCore;
using NMS.Domain.Attendance;
using NMS.Domain.Auditing;
using NMS.Domain.Admissions;
using NMS.Domain.Billing;
using NMS.Domain.Notifications;
using NMS.Domain.Communication;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;

namespace NMS.Application.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; set; }

    bool IsInitialized { get; }
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? RequestId { get; }

    bool IsInRole(string role);

    bool HasPermission(string permission);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<AppUser> AppUsers { get; }

    DbSet<Role> Roles { get; }

    DbSet<AppUserRole> AppUserRoles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<LoginDirectoryEntry> LoginDirectory { get; }

    DbSet<Student> Students { get; }

    DbSet<Guardian> Guardians { get; }

    DbSet<StudentGuardian> StudentGuardians { get; }

    DbSet<Staff> Staff { get; }

    DbSet<StaffRoomAssignment> StaffRoomAssignments { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Program> Programs { get; }

    DbSet<StudentGroup> StudentGroups { get; }

    DbSet<Label> Labels { get; }

    DbSet<StudentLabel> StudentLabels { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<AttendanceEvent> AttendanceEvents { get; }

    DbSet<RatioBreach> RatioBreaches { get; }

    DbSet<LeaveType> LeaveTypes { get; }

    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<MediaFile> MediaFiles { get; }

    DbSet<ActivityPost> ActivityPosts { get; }

    DbSet<ActivityPostStudent> ActivityPostStudents { get; }

    DbSet<ActivityPostMedia> ActivityPostMedia { get; }

    DbSet<DailyReport> DailyReports { get; }

    DbSet<MessageThread> MessageThreads { get; }

    DbSet<ThreadParticipant> ThreadParticipants { get; }

    DbSet<Message> Messages { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<NotificationDelivery> NotificationDeliveries { get; }

    DbSet<NotificationPreference> NotificationPreferences { get; }

    DbSet<DeviceToken> DeviceTokens { get; }

    DbSet<NMS.Application.Feedback.FeedbackNote> FeedbackNotes { get; }

    DbSet<NMS.Application.Reporting.AuditRetentionSetting> AuditRetentionSettings { get; }

    DbSet<NMS.Application.Reporting.AuditRetentionRun> AuditRetentionRuns { get; }

    DbSet<FeePlan> FeePlans { get; }

    DbSet<FeeComponent> FeeComponents { get; }

    DbSet<StudentFeePlan> StudentFeePlans { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<InvoiceLine> InvoiceLines { get; }

    DbSet<InvoicePayment> InvoicePayments { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<NMS.Domain.Forms.Form> Forms { get; }
    DbSet<NMS.Domain.Forms.FormVersion> FormVersions { get; }
    DbSet<NMS.Domain.Forms.FormStart> FormStarts { get; }
    DbSet<NMS.Domain.Forms.FormSubmission> FormSubmissions { get; }
    DbSet<NMS.Domain.Forms.AdmissionStep> AdmissionSteps { get; }
    DbSet<NMS.Domain.Forms.CertificateIssued> CertificatesIssued { get; }
    DbSet<NMS.Domain.Forms.ReportRun> ReportRuns { get; }
    DbSet<NMS.Domain.Communication.Newsletter> Newsletters { get; }
    DbSet<NMS.Domain.Communication.NewsletterRecipient> NewsletterRecipients { get; }
    DbSet<NMS.Domain.Communication.NewsletterLink> NewsletterLinks { get; }
    DbSet<NMS.Domain.Communication.NewsletterClick> NewsletterClicks { get; }
    DbSet<NMS.Domain.Communication.ResourceFolder> ResourceFolders { get; }
    DbSet<NMS.Domain.Communication.ResourceDocument> ResourceDocuments { get; }
    DbSet<NMS.Domain.Communication.ResourceVersion> ResourceVersions { get; }
    DbSet<NMS.Domain.Communication.ResourceDownload> ResourceDownloads { get; }

    DbSet<NMS.Domain.Learning.CurriculumFramework> CurriculumFrameworks { get; }
    DbSet<NMS.Domain.Learning.DevelopmentArea> DevelopmentAreas { get; }
    DbSet<NMS.Domain.Learning.AgeBand> AgeBands { get; }
    DbSet<NMS.Domain.Learning.Milestone> Milestones { get; }
    DbSet<NMS.Domain.Learning.Lesson> Lessons { get; }
    DbSet<NMS.Domain.Forms.StudentAdmissionStep> StudentAdmissionSteps { get; }
    DbSet<NMS.Domain.Calendar.CalendarEvent> CalendarEvents { get; }
    DbSet<CentreSettings> CentreSettings { get; }
    DbSet<Enquiry> Enquiries { get; }
    DbSet<EnquiryStageLog> EnquiryStageLogs { get; }
    DbSet<EnquiryActivity> EnquiryActivities { get; }
    DbSet<Tour> Tours { get; }
    DbSet<TourBlockOff> TourBlockOffs { get; }
    DbSet<FollowUpTask> FollowUpTasks { get; }
    DbSet<OutboundEmail> OutboundEmails { get; }
    DbSet<BillingSettings> BillingSettings { get; }
    DbSet<ReminderLog> ReminderLogs { get; }

    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>Removes history older than the cutoff. The only way history can ever be deleted; the database enforces a minimum age.</summary>
    Task<int> PurgeAuditLogsAsync(DateTimeOffset before, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Restricts what the current user may see: teachers to the rooms they are assigned to,
/// parents to their own children (spec 09 §2.4). Returns null when the user is unrestricted.
/// </summary>
public interface IAccessScope
{
    Task<IReadOnlyList<Guid>?> GetVisibleRoomIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>?> GetOwnStudentIdsAsync(CancellationToken cancellationToken);
}
