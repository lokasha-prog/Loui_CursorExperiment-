using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> builder)
    {
        builder.ToTable("AppUserRoles");
        builder.HasKey(r => new { r.AppUserId, r.RoleId });
        builder.HasOne(r => r.Role).WithMany().HasForeignKey(r => r.RoleId);
        builder.HasIndex(r => r.TenantId);
    }
}
