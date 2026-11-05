using SchoMans.Domain.Assignments;

namespace SchoMans.Domain.Tests.Assignments;

public class QuestionTests
{
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();

    private static Question Choice(string[]? options = null, string key = "1", string body = "What is 2 + 2?") =>
        Question.Create(Subject, Teacher, QuestionType.MultipleChoice, QuestionDifficulty.Easy, body, options ?? ["3", "4", "5"], key, "Because 2 + 2 is 4.");

    [Fact]
    public void A_multiple_choice_question_keeps_its_options_and_the_position_of_the_right_one()
    {
        var question = Choice([" 3 ", "4", "5"]);

        Assert.Equal(["3", "4", "5"], question.Options);
        Assert.Equal("1", question.AnswerKey);
        Assert.Equal((Subject, Teacher, QuestionType.MultipleChoice), (question.SubjectId, question.CreatedByTeacherId, question.Type));
    }

    [Fact]
    public void A_short_answer_question_has_a_model_answer_and_no_options()
    {
        var question = Question.Create(Subject, Teacher, QuestionType.ShortAnswer, QuestionDifficulty.Hard, "Name the capital of France.", [], "Paris", "");

        Assert.Empty(question.Options);
        Assert.Equal("Paris", question.AnswerKey);
        Assert.Equal(QuestionDifficulty.Hard, question.Difficulty);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(" 2 ", true)]
    [InlineData("3", false)]
    [InlineData("-1", false)]
    [InlineData("b", false)]
    public void The_answer_key_of_a_multiple_choice_question_is_the_position_of_an_option(string key, bool valid)
    {
        if (valid)
        {
            Assert.Equal(key.Trim(), Choice(key: key).AnswerKey);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => Choice(key: key));
        }
    }

    [Fact]
    public void Options_must_be_two_to_five_different_texts()
    {
        Assert.Throws<ArgumentException>(() => Choice(["only one"], "0"));
        Assert.Throws<ArgumentException>(() => Choice(["a", "b", "c", "d", "e", "f"], "0"));
        Assert.Throws<ArgumentException>(() => Choice(["a", " ", "c"], "0"));
        Assert.Throws<ArgumentException>(() => Choice(["a", "A", "c"], "0"));
        Assert.Throws<ArgumentException>(() => Choice(["a", new string('x', Question.OptionMaxLength + 1)], "0"));
    }

    [Fact]
    public void A_short_answer_question_with_options_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Question.Create(Subject, Teacher, QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Why?", ["a", "b"], "Because", ""));
    }

    [Fact]
    public void A_question_needs_a_text_and_an_answer_key_within_the_limits()
    {
        Assert.Throws<ArgumentException>(() => Choice(body: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Choice(body: new string('q', Question.BodyMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => Question.Create(Subject, Teacher, QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Why?", [], " ", ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => Question.Create(Subject, Teacher, QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Why?", [], "a", new string('e', Question.ExplanationMaxLength + 1)));
    }

    [Fact]
    public void A_question_can_be_changed_into_another_type()
    {
        var question = Choice();

        question.Update(QuestionType.ShortAnswer, QuestionDifficulty.Medium, "Why is the sky blue?", [], "Scattering", "Light scatters.");

        Assert.Equal((QuestionType.ShortAnswer, QuestionDifficulty.Medium, "Scattering"), (question.Type, question.Difficulty, question.AnswerKey));
        Assert.Empty(question.Options);
    }
}
