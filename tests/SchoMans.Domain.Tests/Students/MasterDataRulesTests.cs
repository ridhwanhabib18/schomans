using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Students;

public class MasterDataRulesTests
{
    private static Student NewStudent() => Student.Create(Guid.NewGuid(), "1000000001", Guid.NewGuid(), new DateOnly(2013, 5, 17));

    [Fact]
    public void A_new_student_is_active()
    {
        Assert.Equal(StudentStatus.Active, NewStudent().Status);
    }

    [Fact]
    public void An_active_student_can_move_to_another_classroom()
    {
        var student = NewStudent();
        var target = Guid.NewGuid();

        Assert.True(student.MoveTo(target));
        Assert.Equal(target, student.ClassroomId);
    }

    [Fact]
    public void A_graduated_student_keeps_the_last_classroom_and_cannot_be_moved()
    {
        var student = NewStudent();
        var last = student.ClassroomId;

        Assert.True(student.Graduate());
        Assert.False(student.Graduate());
        Assert.False(student.MoveTo(Guid.NewGuid()));

        Assert.Equal(StudentStatus.Graduated, student.Status);
        Assert.Equal(last, student.ClassroomId);
    }

    [Fact]
    public void Student_details_are_trimmed_and_a_nisn_is_required()
    {
        var student = NewStudent();

        student.UpdateDetails(" 1000000002 ", new DateOnly(2012, 1, 2));

        Assert.Equal("1000000002", student.Nisn);
        Assert.ThrowsAny<ArgumentException>(() => student.UpdateDetails(" ", new DateOnly(2012, 1, 2)));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void A_minimum_passing_score_must_be_between_0_and_100(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Subject.Create("Mathematics", (decimal)score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(75)]
    [InlineData(100)]
    public void The_limits_of_the_minimum_passing_score_are_allowed(int score)
    {
        Assert.Equal(score, Subject.Create(" Mathematics ", score).MinimumPassingScore);
    }

    [Fact]
    public void A_classroom_can_get_another_homeroom_teacher()
    {
        var classroom = Classroom.Create("Class A", Guid.NewGuid(), Guid.NewGuid());
        var teacher = Guid.NewGuid();

        classroom.Update("Class A1", teacher);

        Assert.Equal("Class A1", classroom.Name);
        Assert.Equal(teacher, classroom.HomeroomTeacherId);
    }

    [Fact]
    public void An_academic_year_can_be_activated_and_deactivated()
    {
        var year = AcademicYear.Create("2026/2027", new DateOnly(2026, 7, 13), new DateOnly(2027, 6, 19), isActive: false);

        year.Activate();
        Assert.True(year.IsActive);
        year.Deactivate();
        Assert.False(year.IsActive);
    }

    [Fact]
    public void Term_dates_must_stay_in_order_when_updated()
    {
        var term = Term.Create(Guid.NewGuid(), "Term 1", new DateOnly(2026, 7, 13), new DateOnly(2026, 12, 19), isActive: true);

        Assert.Throws<ArgumentException>(() => term.Update("Term 1", new DateOnly(2026, 12, 19), new DateOnly(2026, 7, 13)));
    }

    [Fact]
    public void Identity_of_an_account_that_has_not_logged_in_can_be_changed()
    {
        var user = User.Create("old@schomans.demo", "Old Name", UserRole.Student);

        var changed = user.TryUpdateIdentity("New@Schomans.Demo", " New Name ");

        Assert.True(changed);
        Assert.Equal("new@schomans.demo", user.Email);
        Assert.Equal("New Name", user.FullName);
    }

    [Fact]
    public void Identity_of_a_linked_account_belongs_to_keycloak_and_cannot_be_changed_here()
    {
        var user = User.Create("a@schomans.demo", "A Name", UserRole.Student, "sub-1");

        var changed = user.TryUpdateIdentity("b@schomans.demo", "B Name");

        Assert.False(changed);
        Assert.Equal("a@schomans.demo", user.Email);
        Assert.Equal("A Name", user.FullName);
        Assert.True(user.IsLinkedToKeycloak);
    }
}
