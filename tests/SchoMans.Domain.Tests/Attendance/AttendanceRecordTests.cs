using SchoMans.Domain.Attendance;

namespace SchoMans.Domain.Tests.Attendance;

public class AttendanceRecordTests
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_scan_records_present_with_the_scan_time()
    {
        var record = AttendanceRecord.Scanned(Session, Student, Now);

        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Equal(Now, record.ScannedAt);
        Assert.False(record.IsManual);
        Assert.Null(record.MarkedById);
    }

    [Fact]
    public void A_student_who_did_not_scan_is_absent_without_a_scan_time()
    {
        var record = AttendanceRecord.MissedScan(Session, Student);

        Assert.Equal(AttendanceStatus.Absent, record.Status);
        Assert.Null(record.ScannedAt);
        Assert.False(record.IsManual);
    }

    [Fact]
    public void The_teacher_can_change_a_status_and_leave_a_note()
    {
        var record = AttendanceRecord.MissedScan(Session, Student);

        record.Mark(AttendanceStatus.Sick, Teacher, "  Letter from home  ");

        Assert.Equal(AttendanceStatus.Sick, record.Status);
        Assert.True(record.IsManual);
        Assert.Equal(Teacher, record.MarkedById);
        Assert.Equal("Letter from home", record.Note);
    }

    [Fact]
    public void A_blank_note_is_stored_as_no_note()
    {
        var record = AttendanceRecord.Marked(Session, Student, AttendanceStatus.Present, Teacher, "   ");

        Assert.Null(record.Note);
        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.True(record.IsManual);
    }

    [Fact]
    public void A_note_has_a_maximum_length()
    {
        var record = AttendanceRecord.MissedScan(Session, Student);

        Assert.Throws<ArgumentException>(() => record.Mark(AttendanceStatus.Excused, Teacher, new string('x', AttendanceRecord.NoteMaxLength + 1)));
    }
}
