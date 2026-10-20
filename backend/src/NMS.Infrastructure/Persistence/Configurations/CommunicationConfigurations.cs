using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Communication;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles");
        builder.Property(m => m.FileName).HasMaxLength(255).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ScanStatus).HasMaxLength(20).IsRequired();
        builder.Property(m => m.ScanNote).HasMaxLength(500);
        builder.Property(m => m.Content).HasColumnType("varbinary(max)");
        builder.HasIndex(m => new { m.TenantId, m.CreatedAt });
    }
}

public sealed class ActivityPostConfiguration : IEntityTypeConfiguration<ActivityPost>
{
    public void Configure(EntityTypeBuilder<ActivityPost> builder)
    {
        builder.ToTable("ActivityPosts");
        builder.Property(a => a.Type).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ApprovalStatus).HasMaxLength(20).IsRequired();
        builder.Property(a => a.RejectionReason).HasMaxLength(500);
        builder.HasIndex(a => new { a.TenantId, a.ApprovalStatus, a.CreatedAt });
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ActivityPosts_Type", "[Type] IN (N'announcement', N'photo', N'video', N'diary', N'food', N'toilet', N'medical', N'mood')");
            t.HasCheckConstraint("CK_ActivityPosts_Status", "[ApprovalStatus] IN (N'pending', N'approved', N'rejected')");
        });
    }
}

public sealed class ActivityPostStudentConfiguration : IEntityTypeConfiguration<ActivityPostStudent>
{
    public void Configure(EntityTypeBuilder<ActivityPostStudent> builder)
    {
        builder.ToTable("ActivityPostStudents");
        builder.HasKey(a => new { a.PostId, a.StudentId });
        builder.HasOne<ActivityPost>().WithMany().HasForeignKey(a => a.PostId);
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(a => a.StudentId);
        builder.HasIndex(a => new { a.TenantId, a.StudentId });
    }
}

public sealed class ActivityPostMediaConfiguration : IEntityTypeConfiguration<ActivityPostMedia>
{
    public void Configure(EntityTypeBuilder<ActivityPostMedia> builder)
    {
        builder.ToTable("ActivityPostMedia");
        builder.HasKey(a => new { a.PostId, a.MediaId });
        builder.HasOne<ActivityPost>().WithMany().HasForeignKey(a => a.PostId);
        builder.HasOne<MediaFile>().WithMany().HasForeignKey(a => a.MediaId);
    }
}

public sealed class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
{
    public void Configure(EntityTypeBuilder<DailyReport> builder)
    {
        builder.ToTable("DailyReports");
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(d => d.StudentId);
        builder.HasIndex(d => new { d.TenantId, d.StudentId, d.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(d => new { d.TenantId, d.CreatedAt });
    }
}

public sealed class MessageThreadConfiguration : IEntityTypeConfiguration<MessageThread>
{
    public void Configure(EntityTypeBuilder<MessageThread> builder)
    {
        builder.ToTable("MessageThreads");
        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => new { t.TenantId, t.CreatedAt });
        builder.HasIndex(t => new { t.TenantId, t.LastMessageAt });
    }
}

public sealed class ThreadParticipantConfiguration : IEntityTypeConfiguration<ThreadParticipant>
{
    public void Configure(EntityTypeBuilder<ThreadParticipant> builder)
    {
        builder.ToTable("ThreadParticipants");
        builder.HasKey(p => new { p.ThreadId, p.UserId });
        builder.HasOne<MessageThread>().WithMany().HasForeignKey(p => p.ThreadId);
        builder.HasIndex(p => new { p.TenantId, p.UserId });
    }
}

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.Property(m => m.Body).HasMaxLength(4000).IsRequired();
        builder.HasOne<MessageThread>().WithMany().HasForeignKey(m => m.ThreadId);
        builder.HasIndex(m => new { m.TenantId, m.ThreadId, m.CreatedAt });
    }
}
