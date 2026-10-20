using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Gender).HasMaxLength(20);
        builder.Property(s => s.Email).HasMaxLength(254).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.JobTitle).HasMaxLength(100);
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Barcode).HasMaxLength(50).IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.AppUserId);
        builder.HasIndex(s => new { s.TenantId, s.Email }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => new { s.TenantId, s.Barcode }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.CreatedAt });
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Staff_Status", "[Status] IN ('active', 'on_leave', 'inactive', 'terminated')"));
    }
}

public sealed class StaffRoomAssignmentConfiguration : IEntityTypeConfiguration<StaffRoomAssignment>
{
    public void Configure(EntityTypeBuilder<StaffRoomAssignment> builder)
    {
        builder.ToTable("StaffRoomAssignments");
        builder.HasOne<Staff>().WithMany().HasForeignKey(a => a.StaffId);
        builder.HasOne<Room>().WithMany().HasForeignKey(a => a.RoomId);
        builder.HasIndex(a => new { a.TenantId, a.StaffId });
        builder.HasIndex(a => new { a.TenantId, a.RoomId });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StaffRoomAssignments_Day", "[DayOfWeek] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_StaffRoomAssignments_Time", "[EndTime] > [StartTime]");
        });
    }
}
