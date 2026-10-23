using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();

        builder.HasOne(t => t.AcademicYear).WithMany().HasForeignKey(t => t.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.AcademicYearId, t.IsActive });
        builder.HasIndex(t => new { t.AcademicYearId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
