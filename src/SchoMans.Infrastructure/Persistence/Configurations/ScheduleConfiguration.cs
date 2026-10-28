using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.Property(s => s.DayOfWeek).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasOne(s => s.Classroom).WithMany().HasForeignKey(s => s.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Subject).WithMany().HasForeignKey(s => s.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Teacher).WithMany().HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.Restrict);

        // The overlap checks and "today's lessons" look up a classroom's or a teacher's day.
        builder.HasIndex(s => new { s.ClassroomId, s.DayOfWeek, s.StartTime });
        builder.HasIndex(s => new { s.TeacherId, s.DayOfWeek, s.StartTime });
    }
}
