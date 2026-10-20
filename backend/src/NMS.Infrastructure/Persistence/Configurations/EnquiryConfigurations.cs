using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Admissions;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class EnquiryConfiguration : IEntityTypeConfiguration<Enquiry>
{
    public void Configure(EntityTypeBuilder<Enquiry> builder)
    {
        builder.ToTable("Enquiries");
        builder.Property(e => e.FamilyName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ChildName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.Email).HasMaxLength(254);
        builder.Property(e => e.PreferredContact).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Stage).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.HasOne<NMS.Domain.CentreManagement.Program>().WithMany().HasForeignKey(e => e.ProgramId);
        builder.HasOne<NMS.Domain.CentreManagement.Staff>().WithMany().HasForeignKey(e => e.AssignedStaffId);
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(e => e.StudentId);
        builder.HasIndex(e => new { e.TenantId, e.Stage });
        builder.HasIndex(e => new { e.TenantId, e.EnquiryDate });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Enquiries_Stage", "[Stage] IN (N'no_stage', N'interested', N'contacted', N'tour_appointment', N'toured', N'enrolled')");
            t.HasCheckConstraint("CK_Enquiries_Source", "[Source] IN (N'walk_in', N'website', N'referral', N'social_media', N'phone', N'email', N'existing_parent')");
        });
    }
}

public sealed class EnquiryStageLogConfiguration : IEntityTypeConfiguration<EnquiryStageLog>
{
    public void Configure(EntityTypeBuilder<EnquiryStageLog> builder)
    {
        builder.ToTable("EnquiryStageLogs");
        builder.Property(e => e.FromStage).HasMaxLength(30);
        builder.Property(e => e.ToStage).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasOne<Enquiry>().WithMany().HasForeignKey(e => e.EnquiryId);
        builder.HasIndex(e => new { e.TenantId, e.EnquiryId, e.CreatedAt });
    }
}

public sealed class EnquiryActivityConfiguration : IEntityTypeConfiguration<EnquiryActivity>
{
    public void Configure(EntityTypeBuilder<EnquiryActivity> builder)
    {
        builder.ToTable("EnquiryActivities");
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(1000).IsRequired();
        builder.HasOne<Enquiry>().WithMany().HasForeignKey(e => e.EnquiryId);
        builder.HasIndex(e => new { e.TenantId, e.EnquiryId, e.CreatedAt });
    }
}

public sealed class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("Tours");
        builder.Property(t => t.TourType).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Status).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.HasOne<Enquiry>().WithMany().HasForeignKey(t => t.EnquiryId);
        builder.HasOne<NMS.Domain.CentreManagement.Staff>().WithMany().HasForeignKey(t => t.StaffId);
        builder.HasIndex(t => new { t.TenantId, t.StartsAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Tours_Status", "[Status] IN (N'scheduled', N'completed', N'cancelled', N'no_show')");
            t.HasCheckConstraint("CK_Tours_TourType", "[TourType] IN (N'general', N'program_focus', N'birthday_party')");
            t.HasCheckConstraint("CK_Tours_Counts", "[Adults] >= 0 AND [Children] >= 0 AND [DurationMinutes] > 0");
        });
    }
}

public sealed class TourBlockOffConfiguration : IEntityTypeConfiguration<TourBlockOff>
{
    public void Configure(EntityTypeBuilder<TourBlockOff> builder)
    {
        builder.ToTable("TourBlockOffs");
        builder.Property(b => b.Reason).HasMaxLength(300);
        builder.HasOne<NMS.Domain.CentreManagement.Staff>().WithMany().HasForeignKey(b => b.StaffId);
        builder.HasIndex(b => new { b.TenantId, b.StartsAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_TourBlockOffs_Range", "[EndsAt] > [StartsAt]"));
    }
}

public sealed class FollowUpTaskConfiguration : IEntityTypeConfiguration<FollowUpTask>
{
    public void Configure(EntityTypeBuilder<FollowUpTask> builder)
    {
        builder.ToTable("FollowUpTasks");
        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();
        builder.HasOne<NMS.Domain.CentreManagement.Staff>().WithMany().HasForeignKey(t => t.AssignedStaffId);
        builder.HasOne<Enquiry>().WithMany().HasForeignKey(t => t.EnquiryId);
        builder.HasOne<Tour>().WithMany().HasForeignKey(t => t.TourId);
        builder.HasIndex(t => new { t.TenantId, t.IsDone, t.DueDate });
    }
}

public sealed class OutboundEmailConfiguration : IEntityTypeConfiguration<OutboundEmail>
{
    public void Configure(EntityTypeBuilder<OutboundEmail> builder)
    {
        builder.ToTable("OutboundEmails");
        builder.Property(e => e.ToAddress).HasMaxLength(254).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Error).HasMaxLength(500);
        builder.HasIndex(e => new { e.TenantId, e.Status, e.NextAttemptAt });
    }
}

public sealed class CentreSettingsConfiguration : IEntityTypeConfiguration<NMS.Domain.Identity.CentreSettings>
{
    public void Configure(EntityTypeBuilder<NMS.Domain.Identity.CentreSettings> builder)
    {
        builder.ToTable("CentreSettings");
        builder.Property(s => s.Address).HasMaxLength(300);
        builder.Property(s => s.Website).HasMaxLength(200);
        builder.Property(s => s.BrandColor).HasMaxLength(7);
        builder.HasIndex(s => s.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CentreSettings_Late", "[LateGraceMinutes] IS NULL OR [LateGraceMinutes] BETWEEN 0 AND 120");
            t.HasCheckConstraint("CK_CentreSettings_Early", "[EarlyLeaveGraceMinutes] IS NULL OR [EarlyLeaveGraceMinutes] BETWEEN 0 AND 120");
        });
    }
}
