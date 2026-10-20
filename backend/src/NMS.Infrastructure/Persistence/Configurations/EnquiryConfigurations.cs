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
