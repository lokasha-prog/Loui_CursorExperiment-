using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Forms;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("Forms");
        builder.Property(f => f.Type).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(f => new { f.TenantId, f.Status });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Forms_Type", "[Type] IN (N'enquiry', N'admission', N'check_in_out', N'incident', N'custom')");
            t.HasCheckConstraint("CK_Forms_Status", "[Status] IN (N'draft', N'live', N'archived')");
        });
    }
}

public sealed class FormVersionConfiguration : IEntityTypeConfiguration<FormVersion>
{
    public void Configure(EntityTypeBuilder<FormVersion> builder)
    {
        builder.ToTable("FormVersions");
        builder.HasOne<Form>().WithMany().HasForeignKey(v => v.FormId);
        builder.HasIndex(v => new { v.TenantId, v.FormId, v.Version }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class FormStartConfiguration : IEntityTypeConfiguration<FormStart>
{
    public void Configure(EntityTypeBuilder<FormStart> builder)
    {
        builder.ToTable("FormStarts");
        builder.Property(s => s.LastFieldId).HasMaxLength(60);
        builder.HasOne<Form>().WithMany().HasForeignKey(s => s.FormId);
        builder.HasIndex(s => new { s.TenantId, s.FormId, s.CreatedAt });
    }
}

public sealed class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        builder.ToTable("FormSubmissions");
        builder.HasOne<Form>().WithMany().HasForeignKey(s => s.FormId);
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(s => s.StudentId);
        builder.HasOne<NMS.Domain.Admissions.Enquiry>().WithMany().HasForeignKey(s => s.EnquiryId);
        builder.HasIndex(s => new { s.TenantId, s.FormId, s.CreatedAt });
    }
}
