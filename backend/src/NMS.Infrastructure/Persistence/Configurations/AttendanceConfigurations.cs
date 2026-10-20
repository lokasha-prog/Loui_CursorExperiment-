using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Attendance;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.Ignore(a => a.IsPresentNow);
        builder.Property(a => a.PersonType).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Status).HasMaxLength(20).IsRequired();
        builder.Property(a => a.CheckInMethod).HasMaxLength(20);
        builder.Property(a => a.CheckOutMethod).HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.HasIndex(a => new { a.TenantId, a.Date });
        builder.HasIndex(a => new { a.TenantId, a.PersonType, a.PersonId, a.Date });
        builder.HasIndex(a => new { a.TenantId, a.RoomId, a.Date });
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AttendanceRecords_PersonType", "[PersonType] IN (N'student', N'staff')");
            t.HasCheckConstraint("CK_AttendanceRecords_Status", "[Status] IN (N'pending', N'present', N'absent', N'late', N'early_leave')");
            t.HasCheckConstraint("CK_AttendanceRecords_Times", "[CheckOutAt] IS NULL OR [CheckInAt] IS NULL OR [CheckOutAt] >= [CheckInAt]");
        });
    }
}

public sealed class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> builder)
    {
        builder.ToTable("AttendanceEvents");
        builder.Property(a => a.PersonType).HasMaxLength(10).IsRequired();
        builder.Property(a => a.BarcodeScanned).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EventType).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Outcome).HasMaxLength(10).IsRequired();
        builder.Property(a => a.ScannerDevice).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.Message).HasMaxLength(500);
        builder.HasIndex(a => new { a.TenantId, a.ScannedAt });
        builder.HasIndex(a => new { a.TenantId, a.PersonId, a.ScannedAt });
        builder.ToTable(t => t.HasTrigger("TR_AttendanceEvents_Immutable"));
    }
}

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveTypes");
        builder.Property(l => l.AppliesTo).HasMaxLength(10).IsRequired();
        builder.HasIndex(l => new { l.TenantId, l.CreatedAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_LeaveTypes_AppliesTo", "[AppliesTo] IN (N'student', N'staff', N'both')"));
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");
        builder.Property(l => l.PersonType).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Status).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Reason).HasMaxLength(1000);
        builder.Property(l => l.ApprovalNotes).HasMaxLength(1000);
        builder.HasOne(l => l.LeaveType).WithMany().HasForeignKey(l => l.LeaveTypeId);
        builder.HasIndex(l => new { l.TenantId, l.Status });
        builder.HasIndex(l => new { l.TenantId, l.PersonType, l.PersonId, l.StartDate });
        builder.HasIndex(l => new { l.TenantId, l.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LeaveRequests_Status", "[Status] IN (N'submitted', N'approved', N'rejected', N'cancelled')");
            t.HasCheckConstraint("CK_LeaveRequests_Dates", "[EndDate] >= [StartDate]");
        });
    }
}
