using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class ProgressSummaryConfiguration : IEntityTypeConfiguration<ProgressSummary>
{
    public void Configure(EntityTypeBuilder<ProgressSummary> builder)
    {
        builder.Property(s => s.Month).HasMaxLength(7).IsRequired();
        builder.Property(s => s.Body).HasMaxLength(ProgressSummary.BodyMaxLength).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ApprovedById).OnDelete(DeleteBehavior.Restrict);

        // One summary per student per month; the job skips a student who already has one.
        builder.HasIndex(s => new { s.StudentId, s.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => new { s.Month, s.Status });
    }
}
