using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.Property(s => s.Nisn).HasMaxLength(10).IsRequired();

        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Classroom).WithMany().HasForeignKey(s => s.ClassroomId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.Nisn).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.UserId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.ClassroomId);
    }
}
