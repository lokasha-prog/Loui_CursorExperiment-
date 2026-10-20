using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.Property(t => t.DeploymentMode).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Status).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ContactEmail).HasMaxLength(254);
        builder.Property(t => t.ContactPhone).HasMaxLength(50);
        builder.Property(t => t.KhdaLicenseNumber).HasMaxLength(100);
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasQueryFilter(t => !t.IsDeleted);
        builder.ToTable(t => t.HasCheckConstraint("CK_Tenants_DeploymentMode", "[DeploymentMode] IN ('onprem', 'saas')"));
    }
}
