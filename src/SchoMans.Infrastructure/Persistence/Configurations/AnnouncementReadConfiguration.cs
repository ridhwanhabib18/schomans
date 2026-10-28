using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.HasOne<Announcement>().WithMany().HasForeignKey(r => r.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

        // One read per user and announcement: opening it twice records it once.
        builder.HasIndex(r => new { r.AnnouncementId, r.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => r.UserId);
    }
}
