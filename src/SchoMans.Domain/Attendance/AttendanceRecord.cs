using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Attendance;

/// <summary>
/// The attendance of one student in one session (the PRD's <c>Attendance</c> entity; named AttendanceRecord in code
/// because <c>Attendance</c> is the namespace). A QR scan creates it as Present; the teacher can set any status by hand
/// (a phone without a camera, a note from home); closing the session marks everybody without a record Absent.
/// </summary>
public sealed class AttendanceRecord : BaseEntity
{
    public const int NoteMaxLength = 300;

    private AttendanceRecord()
    {
    }

    public Guid SessionId { get; private set; }

    public AttendanceSession? Session { get; private set; }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    public AttendanceStatus Status { get; private set; }

    /// <summary>When the student scanned the QR code; null for a record set by hand or by closing the session.</summary>
    public DateTimeOffset? ScannedAt { get; private set; }

    public bool IsManual { get; private set; }

    /// <summary>The user who set the status by hand, if any.</summary>
    public Guid? MarkedById { get; private set; }

    public string? Note { get; private set; }

    public static AttendanceRecord Scanned(Guid sessionId, Guid studentId, DateTimeOffset now) => new()
    {
        SessionId = sessionId,
        StudentId = studentId,
        Status = AttendanceStatus.Present,
        ScannedAt = now,
    };

    /// <summary>Recorded when the session closes for a student who never scanned.</summary>
    public static AttendanceRecord MissedScan(Guid sessionId, Guid studentId) => new()
    {
        SessionId = sessionId,
        StudentId = studentId,
        Status = AttendanceStatus.Absent,
    };

    public static AttendanceRecord Marked(Guid sessionId, Guid studentId, AttendanceStatus status, Guid markedById, string? note)
    {
        var record = new AttendanceRecord { SessionId = sessionId, StudentId = studentId };
        record.Mark(status, markedById, note);
        return record;
    }

    /// <summary>The teacher sets the status by hand, for example Sick instead of Absent.</summary>
    public void Mark(AttendanceStatus status, Guid markedById, string? note)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > NoteMaxLength)
        {
            throw new ArgumentException($"A note has at most {NoteMaxLength} characters.", nameof(note));
        }

        Status = status;
        IsManual = true;
        MarkedById = markedById;
        Note = trimmed;
    }
}
