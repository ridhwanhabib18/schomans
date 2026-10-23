using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Students;

public class SchoolStructureTests
{
    private static readonly DateOnly Start = new(2026, 7, 13);

    [Fact]
    public void AcademicYear_must_end_after_it_starts()
    {
        Assert.Throws<ArgumentException>(() => AcademicYear.Create("2026/2027", Start, Start, isActive: true));
    }

    [Fact]
    public void Term_must_end_after_it_starts()
    {
        Assert.Throws<ArgumentException>(() => Term.Create(Guid.NewGuid(), "Term 1", Start, Start.AddDays(-1), isActive: true));
    }

    [Fact]
    public void Valid_year_and_term_are_created()
    {
        var year = AcademicYear.Create(" 2026/2027 ", Start, Start.AddMonths(11), isActive: true);
        var term = Term.Create(year.Id, "Term 1", Start, Start.AddMonths(5), isActive: true);

        Assert.Equal("2026/2027", year.Name);
        Assert.Equal(year.Id, term.AcademicYearId);
    }

    [Fact]
    public void Student_requires_a_nisn()
    {
        Assert.ThrowsAny<ArgumentException>(() => Student.Create(Guid.NewGuid(), " ", Guid.NewGuid(), new DateOnly(2013, 1, 1)));
    }

    [Fact]
    public void Teacher_requires_a_nip()
    {
        Assert.ThrowsAny<ArgumentException>(() => Teacher.Create(Guid.NewGuid(), ""));
    }

    [Fact]
    public void Classroom_keeps_its_homeroom_teacher()
    {
        var teacherId = Guid.NewGuid();

        var classroom = Classroom.Create("Class A", Guid.NewGuid(), teacherId);

        Assert.Equal(teacherId, classroom.HomeroomTeacherId);
    }

    [Fact]
    public void New_entities_get_distinct_ids()
    {
        var a = StudentParent.Create(Guid.NewGuid(), Guid.NewGuid());
        var b = StudentParent.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotEqual(a.Id, b.Id);
    }
}
