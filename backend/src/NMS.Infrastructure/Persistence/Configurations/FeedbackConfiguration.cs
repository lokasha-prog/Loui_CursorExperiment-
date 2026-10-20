using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Application.Feedback;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackNote>
{
    public void Configure(EntityTypeBuilder<FeedbackNote> builder)
    {
        builder.ToTable("FeedbackNotes");
        builder.Property(f => f.Kind).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Message).HasMaxLength(2000).IsRequired();
        builder.Property(f => f.Page).HasMaxLength(200);
        builder.Property(f => f.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(f => new { f.TenantId, f.Status, f.CreatedAt });
        builder.HasIndex(f => new { f.TenantId, f.CreatedAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_FeedbackNotes_Rating", "[Rating] IS NULL OR ([Rating] BETWEEN 1 AND 5)"));
    }
}
