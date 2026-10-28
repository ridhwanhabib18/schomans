using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Students;

public class ScheduleTests
{
    private static Schedule Lesson(int fromHour, int toHour, DayOfWeek day = DayOfWeek.Monday) =>
        Schedule.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), day, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0));

    [Fact]
    public void A_lesson_must_end_after_it_starts()
    {
        Assert.Throws<ArgumentException>(() => Lesson(9, 9));
        Assert.Throws<ArgumentException>(() => Lesson(10, 9));
    }

    [Theory]
    [InlineData(8, 10, true)] // starts before, ends inside
    [InlineData(9, 10, true)] // same start
    [InlineData(11, 12, false)] // starts when the other ends
    [InlineData(7, 9, false)] // ends when the other starts
    [InlineData(7, 12, true)] // wraps the other
    [InlineData(10, 12, true)] // starts inside
    public void Lessons_overlap_only_when_they_share_time_on_the_same_day(int fromHour, int toHour, bool overlaps)
    {
        var lesson = Lesson(9, 11);

        Assert.Equal(overlaps, lesson.Overlaps(DayOfWeek.Monday, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0)));
        Assert.False(lesson.Overlaps(DayOfWeek.Tuesday, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0)));
    }

    [Fact]
    public void An_update_moves_the_lesson_but_keeps_the_classroom()
    {
        var lesson = Lesson(9, 11);
        var classroom = lesson.ClassroomId;
        var teacher = Guid.NewGuid();

        lesson.Update(Guid.NewGuid(), teacher, DayOfWeek.Friday, new TimeOnly(13, 0), new TimeOnly(14, 30));

        Assert.Equal(classroom, lesson.ClassroomId);
        Assert.Equal(teacher, lesson.TeacherId);
        Assert.Equal(DayOfWeek.Friday, lesson.DayOfWeek);
        Assert.Equal(new TimeOnly(14, 30), lesson.EndTime);
    }
}
