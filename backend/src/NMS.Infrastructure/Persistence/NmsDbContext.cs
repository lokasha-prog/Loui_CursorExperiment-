using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NMS.Application.Abstractions;
using NMS.Domain.Attendance;
using NMS.Domain.Auditing;
using NMS.Domain.Admissions;
using NMS.Domain.Billing;
using NMS.Domain.Notifications;
using NMS.Domain.Communication;
using NMS.Domain.CentreManagement;
using NMS.Domain.Common;
using NMS.Domain.Identity;
using NMS.Infrastructure.Security;

namespace NMS.Infrastructure.Persistence;

public sealed class NmsDbContext(
    DbContextOptions<NmsDbContext> options,
    ITenantContext tenantContext,
    IEncryptionService encryption) : DbContext(options), IAppDbContext
{
    /// <summary>Read by the query filters and by the session-context interceptor.</summary>
    public Guid CurrentTenantId => tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginDirectoryEntry> LoginDirectory => Set<LoginDirectoryEntry>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();

    public DbSet<Staff> Staff => Set<Staff>();

    public DbSet<StaffRoomAssignment> StaffRoomAssignments => Set<StaffRoomAssignment>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Program> Programs => Set<Program>();

    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<StudentLabel> StudentLabels => Set<StudentLabel>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();

    public DbSet<RatioBreach> RatioBreaches => Set<RatioBreach>();

    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<ActivityPost> ActivityPosts => Set<ActivityPost>();

    public DbSet<ActivityPostStudent> ActivityPostStudents => Set<ActivityPostStudent>();

    public DbSet<ActivityPostMedia> ActivityPostMedia => Set<ActivityPostMedia>();

    public DbSet<DailyReport> DailyReports => Set<DailyReport>();

    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();

    public DbSet<ThreadParticipant> ThreadParticipants => Set<ThreadParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    public DbSet<NMS.Application.Feedback.FeedbackNote> FeedbackNotes => Set<NMS.Application.Feedback.FeedbackNote>();

    public DbSet<NMS.Application.Reporting.AuditRetentionSetting> AuditRetentionSettings => Set<NMS.Application.Reporting.AuditRetentionSetting>();

    public DbSet<NMS.Application.Reporting.AuditRetentionRun> AuditRetentionRuns => Set<NMS.Application.Reporting.AuditRetentionRun>();

    public Task<int> PurgeAuditLogsAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
        // The retry strategy must own the whole unit of work when a transaction is opened by hand.
        Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // One connection and one transaction so the permission flag is set only around this single delete.
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await Database.ExecuteSqlRawAsync("EXEC sp_set_session_context @key = N'audit_purge', @value = 1;", cancellationToken);
                var deleted = await AuditLogs.Where(l => l.OccurredAt < before).ExecuteDeleteAsync(cancellationToken);
                await Database.ExecuteSqlRawAsync("EXEC sp_set_session_context @key = N'audit_purge', @value = 0;", cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return deleted;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });

    public DbSet<FeePlan> FeePlans => Set<FeePlan>();

    public DbSet<FeeComponent> FeeComponents => Set<FeeComponent>();

    public DbSet<StudentFeePlan> StudentFeePlans => Set<StudentFeePlan>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<NMS.Domain.Forms.Form> Forms => Set<NMS.Domain.Forms.Form>();
    public DbSet<NMS.Domain.Forms.FormVersion> FormVersions => Set<NMS.Domain.Forms.FormVersion>();
    public DbSet<NMS.Domain.Forms.FormStart> FormStarts => Set<NMS.Domain.Forms.FormStart>();
    public DbSet<NMS.Domain.Forms.FormSubmission> FormSubmissions => Set<NMS.Domain.Forms.FormSubmission>();
    public DbSet<NMS.Domain.Forms.AdmissionStep> AdmissionSteps => Set<NMS.Domain.Forms.AdmissionStep>();
    public DbSet<NMS.Domain.Forms.CertificateIssued> CertificatesIssued => Set<NMS.Domain.Forms.CertificateIssued>();
    public DbSet<NMS.Domain.Forms.ReportRun> ReportRuns => Set<NMS.Domain.Forms.ReportRun>();
    public DbSet<NMS.Domain.Communication.Newsletter> Newsletters => Set<NMS.Domain.Communication.Newsletter>();
    public DbSet<NMS.Domain.Communication.NewsletterRecipient> NewsletterRecipients => Set<NMS.Domain.Communication.NewsletterRecipient>();
    public DbSet<NMS.Domain.Communication.NewsletterLink> NewsletterLinks => Set<NMS.Domain.Communication.NewsletterLink>();
    public DbSet<NMS.Domain.Communication.NewsletterClick> NewsletterClicks => Set<NMS.Domain.Communication.NewsletterClick>();
    public DbSet<NMS.Domain.Communication.ResourceFolder> ResourceFolders => Set<NMS.Domain.Communication.ResourceFolder>();
    public DbSet<NMS.Domain.Communication.ResourceDocument> ResourceDocuments => Set<NMS.Domain.Communication.ResourceDocument>();
    public DbSet<NMS.Domain.Communication.ResourceVersion> ResourceVersions => Set<NMS.Domain.Communication.ResourceVersion>();
    public DbSet<NMS.Domain.Communication.ResourceDownload> ResourceDownloads => Set<NMS.Domain.Communication.ResourceDownload>();

    public DbSet<NMS.Domain.Learning.CurriculumFramework> CurriculumFrameworks => Set<NMS.Domain.Learning.CurriculumFramework>();
    public DbSet<NMS.Domain.Learning.DevelopmentArea> DevelopmentAreas => Set<NMS.Domain.Learning.DevelopmentArea>();
    public DbSet<NMS.Domain.Learning.AgeBand> AgeBands => Set<NMS.Domain.Learning.AgeBand>();
    public DbSet<NMS.Domain.Learning.Milestone> Milestones => Set<NMS.Domain.Learning.Milestone>();
    public DbSet<NMS.Domain.Learning.Lesson> Lessons => Set<NMS.Domain.Learning.Lesson>();
    public DbSet<NMS.Domain.Forms.StudentAdmissionStep> StudentAdmissionSteps => Set<NMS.Domain.Forms.StudentAdmissionStep>();
    public DbSet<NMS.Domain.Calendar.CalendarEvent> CalendarEvents => Set<NMS.Domain.Calendar.CalendarEvent>();
    public DbSet<CentreSettings> CentreSettings => Set<CentreSettings>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<EnquiryStageLog> EnquiryStageLogs => Set<EnquiryStageLog>();
    public DbSet<EnquiryActivity> EnquiryActivities => Set<EnquiryActivity>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourBlockOff> TourBlockOffs => Set<TourBlockOff>();
    public DbSet<FollowUpTask> FollowUpTasks => Set<FollowUpTask>();
    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();
    public DbSet<BillingSettings> BillingSettings => Set<BillingSettings>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // JSON text columns for translatable fields and small collections (the SQL Server stand-in for JSONB).
        Json<Dictionary<string, string>>(configurationBuilder);
        Json<List<string>>(configurationBuilder);
        Json<string[]>(configurationBuilder);
        Json<Dictionary<string, AuditFieldChange>>(configurationBuilder);
    }

    private static void Json<T>(ModelConfigurationBuilder configurationBuilder)
        where T : class
    {
        configurationBuilder.Properties<T>()
            .HaveConversion<JsonValueConverter<T>, JsonValueComparer<T>>()
            .HaveColumnType("nvarchar(max)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NmsDbContext).Assembly);

        var filterMethod = typeof(NmsDbContext).GetMethod(nameof(ApplyFilters), BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clr = entityType.ClrType;

            if (typeof(ITenantEntity).IsAssignableFrom(clr))
            {
                filterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
                modelBuilder.Entity(clr).HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            }

            foreach (var property in entityType.GetProperties())
            {
                ConfigureProperty(modelBuilder, clr, property);
            }
        }

        // Soft deletes are the norm, so cascading deletes would never be wanted and risk multiple cascade paths.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private void ApplyFilters<T>(ModelBuilder modelBuilder)
        where T : class
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasQueryFilter("Tenant", e => EF.Property<Guid>(e, "TenantId") == CurrentTenantId);
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
        {
            entity.HasQueryFilter("SoftDelete", e => !EF.Property<bool>(e, "IsDeleted"));
        }
    }

    private void ConfigureProperty(ModelBuilder modelBuilder, Type clr, IMutableProperty property)
    {
        var info = property.PropertyInfo;
        if (info is null)
        {
            return;
        }

        var builder = modelBuilder.Entity(clr).Property(property.Name);

        // Sensitive text is encrypted before it reaches the database (spec 09 §4.3).
        if (property.ClrType == typeof(string) && info.GetCustomAttribute<SensitiveAttribute>() is not null)
        {
            builder.HasConversion(new ValueConverter<string?, string?>(
                v => v == null ? null : encryption.Encrypt(v),
                v => v == null ? null : encryption.Decrypt(v)));
            builder.HasColumnType("nvarchar(max)");
        }
    }
}
