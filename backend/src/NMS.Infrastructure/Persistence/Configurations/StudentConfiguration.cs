using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.FirstNameArabic).HasMaxLength(100);
        builder.Property(s => s.LastNameArabic).HasMaxLength(100);
        builder.Property(s => s.Gender).HasMaxLength(20);
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.StatusReason).HasMaxLength(500);
        builder.Property(s => s.Barcode).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Nationality).HasMaxLength(100);
        builder.Property(s => s.Religion).HasMaxLength(100);
        builder.Property(s => s.PrimaryLanguage).HasMaxLength(50);

        builder.HasOne<Room>().WithMany().HasForeignKey(s => s.RoomId);
        builder.HasOne<StudentGroup>().WithMany().HasForeignKey(s => s.GroupId);
        builder.HasOne<Program>().WithMany().HasForeignKey(s => s.ProgramId);
        builder.HasOne<Staff>().WithMany().HasForeignKey(s => s.KeyPersonStaffId);
        builder.HasMany(s => s.StudentGuardians).WithOne(g => g.Student).HasForeignKey(g => g.StudentId);
        builder.HasMany(s => s.Labels).WithOne().HasForeignKey(l => l.StudentId);

        builder.HasIndex(s => new { s.TenantId, s.Barcode }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.CreatedAt });
        builder.HasIndex(s => new { s.TenantId, s.Status }).HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => new { s.TenantId, s.RoomId }).HasFilter("[IsDeleted] = 0");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Students_Status",
                "[Status] IN ('signed_up', 'active', 'on_hold', 'inactive', 'withdrawn', 'transferred')");
            t.HasCheckConstraint(
                "CK_Students_Gender",
                "[Gender] IS NULL OR [Gender] IN ('male', 'female', 'other', 'unspecified')");
        });
    }
}
