using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        // The PRD's Attendance entity.
        builder.ToTable("Attendances");

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(AttendanceRecord.NoteMaxLength);

        builder.HasOne(r => r.Session).WithMany().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.MarkedById).OnDelete(DeleteBehavior.Restrict);

        // Scanning twice is harmless: the second insert is refused and the first record stays.
        builder.HasIndex(r => new { r.SessionId, r.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => r.StudentId);
    }
}
