using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Application.Reporting;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class AuditRetentionSettingConfiguration : IEntityTypeConfiguration<AuditRetentionSetting>
{
    public void Configure(EntityTypeBuilder<AuditRetentionSetting> builder)
    {
        builder.ToTable("AuditRetentionSettings");
        builder.HasIndex(s => s.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t => t.HasCheckConstraint("CK_AuditRetentionSettings_Days", "[RetentionDays] >= 365"));
    }
}

public sealed class AuditRetentionRunConfiguration : IEntityTypeConfiguration<AuditRetentionRun>
{
    public void Configure(EntityTypeBuilder<AuditRetentionRun> builder)
    {
        builder.ToTable("AuditRetentionRuns");
        builder.Property(r => r.Mode).HasMaxLength(10).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.RunAt });
        builder.HasIndex(r => new { r.TenantId, r.CreatedAt });
    }
}
