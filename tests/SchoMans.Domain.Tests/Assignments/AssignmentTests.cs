using SchoMans.Domain.Assignments;
using SchoMans.Domain.Grading;

namespace SchoMans.Domain.Tests.Assignments;

public class AssignmentTests
{
    private static readonly DateTimeOffset Due = new(2026, 11, 2, 16, 59, 0, TimeSpan.Zero);
    private static readonly Guid ClassA = Guid.NewGuid();
    private static readonly Guid ClassB = Guid.NewGuid();

    private static Assignment New(params Guid[] classrooms) =>
        Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Fractions  ", "  Page 12  ", Due, classrooms.Length == 0 ? [ClassA] : classrooms);

    [Fact]
    public void A_new_assignment_is_a_regular_assignment_component_with_trimmed_text()
    {
        var assignment = New();

        Assert.Equal("Fractions", assignment.Title);
        Assert.Equal("Page 12", assignment.Description);
        Assert.Equal(AssignmentType.Regular, assignment.Type);
        Assert.Equal(GradeComponent.Assignment, assignment.Component);
        Assert.Equal([ClassA], assignment.Classrooms.Select(c => c.ClassroomId));
        Assert.All(assignment.Classrooms, c => Assert.Equal(assignment.Id, c.AssignmentId));
    }

    [Fact]
    public void It_is_given_to_at_least_one_classroom_and_never_twice_to_the_same()
    {
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T", "", Due, []));

        var assignment = New(ClassA, ClassA, ClassB);

        Assert.Equal(2, assignment.Classrooms.Count);
    }

    [Fact]
    public void Setting_classrooms_keeps_the_ones_that_stay_and_replaces_the_others()
    {
        var assignment = New(ClassA, ClassB);
        var classC = Guid.NewGuid();
        var kept = assignment.Classrooms.Single(c => c.ClassroomId == ClassB);

        assignment.SetClassrooms([ClassB, classC]);

        Assert.Equal([ClassB, classC], assignment.Classrooms.Select(c => c.ClassroomId));
        Assert.Same(kept, assignment.Classrooms[0]);
    }

    [Fact]
    public void A_title_is_required()
    {
        Assert.Throws<ArgumentException>(() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ", "", Due, [ClassA]));
    }

    [Fact]
    public void It_takes_up_to_five_attachments()
    {
        var assignment = New();
        for (var i = 0; i < Assignment.MaxAttachments; i++)
        {
            Assert.True(assignment.AddAttachment($"key{i}", $"file{i}.pdf"));
        }

        Assert.False(assignment.AddAttachment("key6", "file6.pdf"));
        Assert.Equal(Assignment.MaxAttachments, assignment.Attachments.Count);
    }

    [Fact]
    public void Removing_an_attachment_returns_it_and_an_unknown_one_returns_nothing()
    {
        var assignment = New();
        assignment.AddAttachment("key", "page.pdf");
        var attachment = assignment.Attachments[0];

        Assert.Same(attachment, assignment.RemoveAttachment(attachment.Id));
        Assert.Empty(assignment.Attachments);
        Assert.Null(assignment.RemoveAttachment(Guid.NewGuid()));
    }

    [Fact]
    public void A_long_file_name_is_cut_to_the_maximum()
    {
        var attachment = AssignmentAttachment.Create(Guid.NewGuid(), "key", new string('a', 300));

        Assert.Equal(AssignmentAttachment.FileNameMaxLength, attachment.FileName.Length);
    }

    [Fact]
    public void Work_is_late_only_after_the_due_moment()
    {
        var assignment = New();

        Assert.False(assignment.IsLateAt(Due));
        Assert.True(assignment.IsLateAt(Due.AddSeconds(1)));
    }
}
