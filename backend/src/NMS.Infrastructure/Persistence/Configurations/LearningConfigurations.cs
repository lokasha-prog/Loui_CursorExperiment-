using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;
using NMS.Domain.Learning;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class CurriculumFrameworkConfiguration : IEntityTypeConfiguration<CurriculumFramework>
{
    public void Configure(EntityTypeBuilder<CurriculumFramework> builder)
    {
        builder.ToTable("CurriculumFrameworks");
        builder.Property(f => f.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(f => new { f.TenantId, f.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DevelopmentAreaConfiguration : IEntityTypeConfiguration<DevelopmentArea>
{
    public void Configure(EntityTypeBuilder<DevelopmentArea> builder)
    {
        builder.ToTable("DevelopmentAreas");
        builder.Property(a => a.Code).HasMaxLength(40).IsRequired();
        builder.HasOne<CurriculumFramework>().WithMany().HasForeignKey(a => a.FrameworkId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(a => new { a.TenantId, a.FrameworkId, a.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class AgeBandConfiguration : IEntityTypeConfiguration<AgeBand>
{
    public void Configure(EntityTypeBuilder<AgeBand> builder)
    {
        builder.ToTable("AgeBands");
        builder.Property(a => a.Code).HasMaxLength(40).IsRequired();
        builder.HasOne<CurriculumFramework>().WithMany().HasForeignKey(a => a.FrameworkId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(a => new { a.TenantId, a.FrameworkId, a.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t => t.HasCheckConstraint("CK_AgeBands_Months", "[MinMonths] >= 0 AND [MaxMonths] > [MinMonths]"));
    }
}

public sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");
        builder.Property(m => m.Code).HasMaxLength(40).IsRequired();
        builder.HasOne<CurriculumFramework>().WithMany().HasForeignKey(m => m.FrameworkId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<DevelopmentArea>().WithMany().HasForeignKey(m => m.AreaId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<AgeBand>().WithMany().HasForeignKey(m => m.AgeBandId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(m => new { m.TenantId, m.FrameworkId, m.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(m => new { m.TenantId, m.AreaId, m.AgeBandId });
    }
}

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasOne<CurriculumFramework>().WithMany().HasForeignKey(l => l.FrameworkId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(l => new { l.TenantId, l.FrameworkId, l.AreaId, l.AgeBandId });
        builder.HasIndex(l => new { l.TenantId, l.RoomId });
        builder.HasIndex(l => new { l.TenantId, l.CreatedAt });
    }
}

public sealed class ObservationConfiguration : IEntityTypeConfiguration<Observation>
{
    public void Configure(EntityTypeBuilder<Observation> builder)
    {
        builder.ToTable("Observations");
        builder.Property(o => o.Type).HasMaxLength(20).IsRequired();
        builder.HasOne<Student>().WithMany().HasForeignKey(o => o.StudentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(o => new { o.TenantId, o.StudentId, o.ObservedAt });
        builder.HasIndex(o => new { o.TenantId, o.CreatedAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_Observations_Type", "[Type] IN (N'formal', N'informal', N'concern')"));
    }
}

public sealed class AssessmentTemplateConfiguration : IEntityTypeConfiguration<AssessmentTemplate>
{
    public void Configure(EntityTypeBuilder<AssessmentTemplate> builder)
    {
        builder.ToTable("AssessmentTemplates");
        builder.Property(t => t.ScoringKind).HasMaxLength(20).IsRequired();
        builder.HasOne<CurriculumFramework>().WithMany().HasForeignKey(t => t.FrameworkId).OnDelete(DeleteBehavior.NoAction);
        builder.ToTable(t => t.HasCheckConstraint("CK_AssessmentTemplates_Scoring", "[ScoringKind] IN (N'binary', N'three_point')"));
    }
}

public sealed class AssessmentRunConfiguration : IEntityTypeConfiguration<AssessmentRun>
{
    public void Configure(EntityTypeBuilder<AssessmentRun> builder)
    {
        builder.ToTable("AssessmentRuns");
        builder.Property(r => r.Term).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.HasOne<AssessmentTemplate>().WithMany().HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(r => new { r.TenantId, r.TemplateId, r.StudentId, r.Term }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.TenantId, r.StudentId, r.AssessedOn });
        builder.ToTable(t => t.HasCheckConstraint("CK_AssessmentRuns_Status", "[Status] IN (N'draft', N'completed')"));
    }
}

public sealed class AssessmentScoreConfiguration : IEntityTypeConfiguration<AssessmentScore>
{
    public void Configure(EntityTypeBuilder<AssessmentScore> builder)
    {
        builder.ToTable("AssessmentScores");
        builder.Property(s => s.Note).HasMaxLength(500);
        builder.HasOne<AssessmentRun>().WithMany().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Milestone>().WithMany().HasForeignKey(s => s.MilestoneId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(s => new { s.TenantId, s.RunId, s.MilestoneId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
