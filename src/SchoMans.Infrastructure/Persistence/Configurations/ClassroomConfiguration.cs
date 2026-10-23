using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();

        builder.HasOne(c => c.AcademicYear).WithMany().HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.HomeroomTeacher).WithMany().HasForeignKey(c => c.HomeroomTeacherId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.AcademicYearId, c.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => c.HomeroomTeacherId);
    }
}
