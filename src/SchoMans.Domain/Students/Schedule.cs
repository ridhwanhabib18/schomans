using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

/// <summary>One weekly lesson in the timetable: a teacher teaches a subject to a classroom on a day, from a time to a time.</summary>
public sealed class Schedule : BaseEntity
{
    private Schedule()
    {
    }

    public Guid ClassroomId { get; private set; }

    public Classroom? Classroom { get; private set; }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public Guid TeacherId { get; private set; }

    public Teacher? Teacher { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    /// <summary>Local school time.</summary>
    public TimeOnly StartTime { get; private set; }

    /// <summary>Local school time.</summary>
    public TimeOnly EndTime { get; private set; }

    public static Schedule Create(Guid classroomId, Guid subjectId, Guid teacherId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        var schedule = new Schedule { ClassroomId = classroomId };
        schedule.Update(subjectId, teacherId, dayOfWeek, startTime, endTime);
        return schedule;
    }

    public void Update(Guid subjectId, Guid teacherId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("A lesson must end after it starts.", nameof(endTime));
        }

        SubjectId = subjectId;
        TeacherId = teacherId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>True when this lesson and the given time range on the given day overlap (touching ends do not overlap).</summary>
    public bool Overlaps(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime) =>
        DayOfWeek == dayOfWeek && StartTime < endTime && startTime < EndTime;
}
