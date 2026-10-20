using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Auditing;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserEmail).HasMaxLength(254);
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(30).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.RequestId).HasMaxLength(100);
        builder.HasIndex(a => new { a.TenantId, a.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(a => new { a.TenantId, a.UserId, a.OccurredAt }).IsDescending(false, false, true);
        builder.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId, a.OccurredAt }).IsDescending(false, false, false, true);
        builder.HasIndex(a => new { a.TenantId, a.Action, a.OccurredAt }).IsDescending(false, false, true);
        builder.ToTable(t => t.HasTrigger("TR_AuditLogs_Immutable"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AuditLogs_Action",
            "[Action] IN ('create', 'update', 'delete', 'restore', 'export', 'login', 'logout', 'permission_change')"));
    }
}
