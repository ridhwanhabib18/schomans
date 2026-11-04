using SchoMans.Domain.Grading;

namespace SchoMans.Domain.Tests.Grading;

public class LearningObjectiveTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();

    [Fact]
    public void An_objective_keeps_its_trimmed_code_and_description()
    {
        var objective = LearningObjective.Create(SubjectId, " 1.1 ", "  Understands fractions ");

        Assert.Equal(SubjectId, objective.SubjectId);
        Assert.Equal("1.1", objective.Code);
        Assert.Equal("Understands fractions", objective.Description);
    }

    [Theory]
    [InlineData(" ", "Something")]
    [InlineData("1.1", " ")]
    public void An_objective_needs_a_code_and_a_description(string code, string description)
    {
        Assert.Throws<ArgumentException>(() => LearningObjective.Create(SubjectId, code, description));
    }

    [Fact]
    public void A_code_and_a_description_have_a_maximum_length()
    {
        Assert.Throws<ArgumentException>(() => LearningObjective.Create(SubjectId, new string('x', LearningObjective.CodeMaxLength + 1), "Ok"));
        Assert.Throws<ArgumentException>(() => LearningObjective.Create(SubjectId, "1.1", new string('x', LearningObjective.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void An_objective_can_be_edited()
    {
        var objective = LearningObjective.Create(SubjectId, "1.1", "Old");

        objective.Update("1.2", "New");

        Assert.Equal("1.2", objective.Code);
        Assert.Equal("New", objective.Description);
    }

    [Fact]
    public void A_grade_can_be_linked_to_an_objective()
    {
        var objectiveId = Guid.NewGuid();

        var linked = Grade.Create(Guid.NewGuid(), SubjectId, Guid.NewGuid(), GradeComponent.Quiz, 80, learningObjectiveId: objectiveId);
        var plain = Grade.Create(Guid.NewGuid(), SubjectId, Guid.NewGuid(), GradeComponent.Quiz, 80);

        Assert.Equal(objectiveId, linked.LearningObjectiveId);
        Assert.Null(plain.LearningObjectiveId);
    }
}

public class AchievementDescriptionDraftTests
{
    private static AchievementDescription Description() => AchievementDescription.Create(Guid.NewGuid(), Guid.NewGuid(), "Teacher text.");

    [Fact]
    public void An_ai_draft_waits_for_review_and_is_marked_as_the_ais()
    {
        var description = Description();
        description.Accept();

        description.Redraft("  The AI's text.  ");

        Assert.Equal("The AI's text.", description.Body);
        Assert.Equal(AchievementStatus.Draft, description.Status);
        Assert.True(description.IsAiDrafted);
    }

    [Fact]
    public void Accepting_an_ai_draft_keeps_it_marked_as_the_ais()
    {
        var description = Description();
        description.Redraft("The AI's text.");

        description.Accept();

        Assert.Equal(AchievementStatus.Accepted, description.Status);
        Assert.True(description.IsAiDrafted);
    }

    [Fact]
    public void Editing_an_ai_draft_makes_it_the_teachers_text()
    {
        var description = Description();
        description.Redraft("The AI's text.");

        description.Write("My own words.");

        Assert.False(description.IsAiDrafted);
        Assert.Equal(AchievementStatus.Draft, description.Status);
    }

    [Fact]
    public void An_ai_draft_is_checked_like_any_text()
    {
        var description = Description();

        Assert.Throws<ArgumentException>(() => description.Redraft(" "));
        Assert.Throws<ArgumentException>(() => description.Redraft(new string('x', AchievementDescription.BodyMaxLength + 1)));
    }
}
