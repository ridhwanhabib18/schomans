using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Attendance;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.Property(s => s.QrSecret).HasMaxLength(32).IsRequired();
        builder.Property(s => s.PreviousQrSecret).HasMaxLength(32);
        builder.Ignore(s => s.IsOpen);
        builder.Ignore(s => s.QrToken);

        builder.HasOne(s => s.Schedule).WithMany().HasForeignKey(s => s.ScheduleId).OnDelete(DeleteBehavior.Restrict);

        // One session per lesson per day, also when two devices open it at the same moment.
        builder.HasIndex(s => new { s.ScheduleId, s.Date }).IsUnique().HasFilter("[IsDeleted] = 0");

        // The rotation and clean-up jobs look for open sessions.
        builder.HasIndex(s => s.ClosedAt);
    }
}
