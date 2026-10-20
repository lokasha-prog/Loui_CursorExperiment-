using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.ToTable("StudentGroups");
        builder.HasOne(g => g.Room).WithMany().HasForeignKey(g => g.RoomId);
        builder.HasIndex(g => new { g.TenantId, g.RoomId });
    }
}

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Labels");
        builder.Property(l => l.Color).HasMaxLength(7).IsRequired();
    }
}

public sealed class StudentLabelConfiguration : IEntityTypeConfiguration<StudentLabel>
{
    public void Configure(EntityTypeBuilder<StudentLabel> builder)
    {
        builder.ToTable("StudentLabels");
        builder.HasKey(l => new { l.StudentId, l.LabelId });
        builder.HasOne<Label>().WithMany().HasForeignKey(l => l.LabelId);
        builder.HasIndex(l => l.TenantId);
    }
}
