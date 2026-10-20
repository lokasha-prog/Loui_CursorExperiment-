using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");
        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.PreferredLanguage).HasMaxLength(5).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(50);
        builder.Property(u => u.Status).HasMaxLength(20).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasMany(u => u.Roles).WithOne(r => r.AppUser).HasForeignKey(r => r.AppUserId);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AppUsers_Status", "[Status] IN ('active', 'suspended', 'invited')");
            t.HasCheckConstraint("CK_AppUsers_Language", "[PreferredLanguage] IN ('en', 'ar')");
        });
    }
}
