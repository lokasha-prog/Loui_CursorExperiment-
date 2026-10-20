using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.CentreManagement;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.HasIndex(r => new { r.TenantId, r.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Rooms_Capacity", "[Capacity] > 0");
            t.HasCheckConstraint("CK_Rooms_Ratio", "[RatioStaffToStudent] > 0");
            t.HasCheckConstraint("CK_Rooms_Age", "[MaxAgeMonths] >= [MinAgeMonths]");
        });
    }
}
