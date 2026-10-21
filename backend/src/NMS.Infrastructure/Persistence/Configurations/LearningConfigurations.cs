using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
