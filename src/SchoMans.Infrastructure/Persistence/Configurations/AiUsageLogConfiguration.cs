using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Ai;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.Property(l => l.Feature).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Provider).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Model).HasMaxLength(100).IsRequired();
        builder.Property(l => l.CostUsd).HasPrecision(18, 6);

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);

        // The daily count of a user and the monthly sum of everybody.
        builder.HasIndex(l => new { l.UserId, l.CreatedAt });
        builder.HasIndex(l => l.CreatedAt);
    }
}
