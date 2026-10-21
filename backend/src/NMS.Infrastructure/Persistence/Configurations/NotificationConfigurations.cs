using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Notifications;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Tier).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Type).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(200);
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.ReadAt });
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.Property(n => n.Channel).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Status).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Recipient).HasMaxLength(254);
        builder.Property(n => n.Error).HasMaxLength(500);
        builder.HasOne<Notification>().WithMany().HasForeignKey(n => n.NotificationId);
        builder.HasIndex(n => new { n.TenantId, n.Status, n.NextAttemptAt });
        builder.HasIndex(n => new { n.TenantId, n.CreatedAt });
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.Property(n => n.Tier).HasMaxLength(20).IsRequired();
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.Tier }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(n => new { n.TenantId, n.CreatedAt });
    }
}

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");
        builder.Property(d => d.Token).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Platform).HasMaxLength(20).IsRequired();
        builder.HasIndex(d => new { d.TenantId, d.Token }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(d => new { d.TenantId, d.UserId });
    }
}
