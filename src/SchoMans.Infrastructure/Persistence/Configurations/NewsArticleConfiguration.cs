using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Communication;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Slug).HasMaxLength(100).IsRequired();
        builder.Property(n => n.Body).IsRequired();
        builder.Property(n => n.CoverImageKey).HasMaxLength(300);
        builder.Property(n => n.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.MetaDescription).HasMaxLength(300);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Not filtered by IsDeleted: a slug is never reused, so an old shared link cannot show other content.
        builder.HasIndex(n => n.Slug).IsUnique();
        builder.HasIndex(n => new { n.Status, n.PublishedAt });
    }
}
