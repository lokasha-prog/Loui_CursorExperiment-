using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Communication;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class NewsletterConfiguration : IEntityTypeConfiguration<Newsletter>
{
    public void Configure(EntityTypeBuilder<Newsletter> builder)
    {
        builder.ToTable("Newsletters");
        builder.Property(n => n.AudienceType).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(n => new { n.TenantId, n.Status, n.ScheduledFor });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Newsletters_Status", "[Status] IN (N'draft', N'scheduled', N'published', N'archived')");
            t.HasCheckConstraint("CK_Newsletters_Audience", "[AudienceType] IN (N'all', N'room', N'program', N'group', N'label')");
        });
    }
}

public sealed class NewsletterRecipientConfiguration : IEntityTypeConfiguration<NewsletterRecipient>
{
    public void Configure(EntityTypeBuilder<NewsletterRecipient> builder)
    {
        builder.ToTable("NewsletterRecipients");
        builder.Property(r => r.Token).HasMaxLength(100).IsRequired();
        builder.HasOne<Newsletter>().WithMany().HasForeignKey(r => r.NewsletterId);
        builder.HasIndex(r => new { r.NewsletterId, r.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => r.Token).IsUnique();
    }
}

public sealed class NewsletterLinkConfiguration : IEntityTypeConfiguration<NewsletterLink>
{
    public void Configure(EntityTypeBuilder<NewsletterLink> builder)
    {
        builder.ToTable("NewsletterLinks");
        builder.Property(l => l.Url).HasMaxLength(2000).IsRequired();
        builder.HasOne<Newsletter>().WithMany().HasForeignKey(l => l.NewsletterId);
        builder.HasIndex(l => new { l.NewsletterId, l.LinkIndex }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class NewsletterClickConfiguration : IEntityTypeConfiguration<NewsletterClick>
{
    public void Configure(EntityTypeBuilder<NewsletterClick> builder)
    {
        builder.ToTable("NewsletterClicks");
        builder.HasOne<Newsletter>().WithMany().HasForeignKey(c => c.NewsletterId);
        builder.HasOne<NewsletterRecipient>().WithMany().HasForeignKey(c => c.RecipientId);
        builder.HasIndex(c => new { c.NewsletterId, c.LinkIndex });
    }
}

public sealed class ResourceFolderConfiguration : IEntityTypeConfiguration<ResourceFolder>
{
    public void Configure(EntityTypeBuilder<ResourceFolder> builder)
    {
        builder.ToTable("ResourceFolders");
        builder.Property(f => f.Audience).HasMaxLength(20).IsRequired();
        builder.HasOne<ResourceFolder>().WithMany().HasForeignKey(f => f.ParentFolderId);
        builder.ToTable(t => t.HasCheckConstraint("CK_ResourceFolders_Audience", "[Audience] IN (N'parents', N'staff')"));
    }
}

public sealed class ResourceDocumentConfiguration : IEntityTypeConfiguration<ResourceDocument>
{
    public void Configure(EntityTypeBuilder<ResourceDocument> builder)
    {
        builder.ToTable("ResourceDocuments");
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.Tags).HasMaxLength(300);
        builder.HasOne<ResourceFolder>().WithMany().HasForeignKey(d => d.FolderId);
        builder.HasIndex(d => new { d.TenantId, d.FolderId });
    }
}

public sealed class ResourceVersionConfiguration : IEntityTypeConfiguration<ResourceVersion>
{
    public void Configure(EntityTypeBuilder<ResourceVersion> builder)
    {
        builder.ToTable("ResourceVersions");
        builder.Property(v => v.FileName).HasMaxLength(200).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(120).IsRequired();
        builder.HasOne<ResourceDocument>().WithMany().HasForeignKey(v => v.DocumentId);
        builder.HasIndex(v => new { v.DocumentId, v.Version }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class ResourceDownloadConfiguration : IEntityTypeConfiguration<ResourceDownload>
{
    public void Configure(EntityTypeBuilder<ResourceDownload> builder)
    {
        builder.ToTable("ResourceDownloads");
        builder.HasOne<ResourceDocument>().WithMany().HasForeignKey(d => d.DocumentId);
        builder.HasOne<ResourceVersion>().WithMany().HasForeignKey(d => d.VersionId);
        builder.HasIndex(d => new { d.TenantId, d.DocumentId, d.CreatedAt });
    }
}
