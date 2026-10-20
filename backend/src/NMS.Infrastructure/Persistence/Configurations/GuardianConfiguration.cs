using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("Guardians");
        builder.Property(g => g.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.LastName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Email).HasMaxLength(254).IsRequired();
        builder.Property(g => g.Phone).HasMaxLength(50);
        builder.Property(g => g.Relationship).HasMaxLength(30).IsRequired();
        builder.Property(g => g.Nationality).HasMaxLength(100);
        builder.Property(g => g.Occupation).HasMaxLength(100);
        builder.Property(g => g.Employer).HasMaxLength(200);
        builder.HasOne(g => g.AppUser).WithMany().HasForeignKey(g => g.AppUserId);
        builder.HasIndex(g => new { g.TenantId, g.Email }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Guardians_Relationship",
            "[Relationship] IN ('mother', 'father', 'grandparent', 'legal_guardian', 'other')"));
    }
}

public sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians");
        builder.HasKey(g => new { g.StudentId, g.GuardianId });
        builder.HasOne(g => g.Guardian).WithMany().HasForeignKey(g => g.GuardianId);
        builder.HasIndex(g => g.TenantId);
        builder.HasIndex(g => g.GuardianId);
    }
}
