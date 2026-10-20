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

public sealed class AdmissionStepConfiguration : IEntityTypeConfiguration<AdmissionStep>
{
    public void Configure(EntityTypeBuilder<AdmissionStep> builder)
    {
        builder.ToTable("AdmissionSteps");
        builder.HasOne<Form>().WithMany().HasForeignKey(s => s.FormId);
        builder.HasIndex(s => new { s.TenantId, s.SortOrder });
        builder.ToTable(t => t.HasCheckConstraint("CK_AdmissionSteps_DueDays", "[DueDays] BETWEEN 0 AND 365"));
    }
}

public sealed class StudentAdmissionStepConfiguration : IEntityTypeConfiguration<StudentAdmissionStep>
{
    public void Configure(EntityTypeBuilder<StudentAdmissionStep> builder)
    {
        builder.ToTable("StudentAdmissionSteps");
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(s => s.StudentId);
        builder.HasOne<AdmissionStep>().WithMany().HasForeignKey(s => s.StepId);
        builder.HasIndex(s => new { s.TenantId, s.StudentId, s.StepId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => new { s.TenantId, s.Status, s.DueDate });
        builder.ToTable(t => t.HasCheckConstraint("CK_StudentAdmissionSteps_Status", "[Status] IN (N'pending', N'current', N'done', N'skipped')"));
    }
}

public sealed class CertificateIssuedConfiguration : IEntityTypeConfiguration<CertificateIssued>
{
    public void Configure(EntityTypeBuilder<CertificateIssued> builder)
    {
        builder.ToTable("CertificatesIssued");
        builder.Property(c => c.TemplateId).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(c => c.StudentId);
        builder.HasIndex(c => new { c.TenantId, c.TemplateId, c.Number }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.StudentId });
    }
}

public sealed class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> builder)
    {
        builder.ToTable("ReportRuns");
        builder.Property(r => r.Kind).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Format).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Error).HasMaxLength(500);
        builder.Property(r => r.FileName).HasMaxLength(200);
        builder.Property(r => r.ContentType).HasMaxLength(100);
        builder.HasIndex(r => new { r.TenantId, r.RequestedBy, r.CreatedAt });
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ReportRuns_Status", "[Status] IN (N'pending', N'running', N'done', N'failed')");
            t.HasCheckConstraint("CK_ReportRuns_Format", "[Format] IN (N'excel', N'pdf', N'json')");
        });
    }
}
