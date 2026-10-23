using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(a => a.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        // At most one active academic year.
        builder.HasIndex(a => a.IsActive).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
    }
}
