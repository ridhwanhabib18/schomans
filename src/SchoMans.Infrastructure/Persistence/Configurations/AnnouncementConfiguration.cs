using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Body).IsRequired();
        builder.Property(a => a.CoverImageKey).HasMaxLength(300);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // The audience lists are stored as JSON arrays in one column and stay queryable (OPENJSON). Roles are stored by name.
        builder.PrimitiveCollection(a => a.TargetRoles)
            .ElementType(element => element.HasConversion<string>().HasMaxLength(32));
        builder.PrimitiveCollection(a => a.TargetClassroomIds);

        builder.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.Status, a.PublishedAt });
        builder.HasIndex(a => new { a.Status, a.IsPublic, a.PublishedAt });
    }
}
