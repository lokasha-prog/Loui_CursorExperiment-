using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("Programs");
        builder.Property(p => p.AgeGroup).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.CreatedAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_Programs_Days", "[DaysPerWeek] BETWEEN 1 AND 7"));
    }
}
