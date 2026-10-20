using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class LoginDirectoryConfiguration : IEntityTypeConfiguration<LoginDirectoryEntry>
{
    public void Configure(EntityTypeBuilder<LoginDirectoryEntry> builder)
    {
        builder.ToTable("LoginDirectory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(e => new { e.Email, e.CentreId }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.CentreId).OnDelete(DeleteBehavior.Restrict);
    }
}
