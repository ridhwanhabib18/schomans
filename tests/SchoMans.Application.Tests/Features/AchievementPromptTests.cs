using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.AchievementDescriptions;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Application.Tests.Features;

/// <summary>
/// The test set of AI-02 (PRD: at least 10 cases, run whenever the prompt changes): what the model is told, and which answers are
/// accepted. Provider answers are scripted here, so the set runs without a network; the same expectations are what a manual run
/// against Ollama or Claude is judged by.
/// </summary>
public class AchievementPromptTests
{
    private static readonly Guid StudentId = Guid.NewGuid();

    private static AchievementFacts Facts(
        decimal? final = 82.5m,
        IReadOnlyList<ObjectiveFact>? objectives = null,
        IReadOnlyList<string>? notes = null) =>
        new(
            "Mathematics",
            "Semester 1",
            75,
            final,
            final is null ? null : "B",
            final is null ? [] : [new ComponentFact("Quiz", 80), new ComponentFact("Midterm", 85)],
            objectives ?? [new ObjectiveFact("1.1", "Understands fractions", 90), new ObjectiveFact("1.2", "Solves word problems", 60)],
            notes ?? []);

    private static (AiRequest Request, NameAliases Aliases) Build(AchievementFacts facts, string name = "Rina Putri", string? extra = null)
    {
        var aliases = new NameAliases();
        var alias = aliases.For(StudentId, name);
        return (AchievementPrompt.Build(facts, alias, aliases, extra), aliases);
    }

    private static string Answer(string achieved = "S1 has shown a good grasp of fractions.", string needsSupport = "S1 will benefit from more practice with word problems.") =>
        System.Text.Json.JsonSerializer.Serialize(new { achieved, needsSupport });

    // ---- what the model is told ----

    [Fact]
    public void The_request_is_for_ai_02_and_asks_for_json()
    {
        var (request, _) = Build(Facts());

        Assert.Equal(AiFeatures.AchievementDescription, request.Feature);
        Assert.True(request.JsonOutput);
        Assert.Contains("JSON", request.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_student_name_never_reaches_the_prompt_only_the_code()
    {
        var (request, _) = Build(Facts(notes: ["Rina Putri explained fractions to a friend."]));

        Assert.Contains("Student: S1", request.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Rina", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Putri", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1 explained fractions", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_carries_the_grades_and_the_scores_per_objective()
    {
        var (request, _) = Build(Facts());

        Assert.Contains("Subject: Mathematics", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Final grade: 82.5", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Average Quiz: 80", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- 1.1 Understands fractions: 90", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("- 1.2 Solves word problems: 60", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void An_objective_without_scores_is_marked_not_assessed()
    {
        var (request, _) = Build(Facts(objectives: [new ObjectiveFact("2.1", "Reads graphs", null)]));

        Assert.Contains("- 2.1 Reads graphs: not assessed", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_subject_without_objectives_says_so_instead_of_inventing_some()
    {
        var (request, _) = Build(Facts(objectives: []));

        Assert.Contains("Learning objectives: none defined", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_system_prompt_forbids_inventing_labelling_and_quoting_notes()
    {
        Assert.Contains("Never invent", AchievementPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Never label the student", AchievementPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("never quote", AchievementPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("not been assessed", AchievementPrompt.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void An_extra_instruction_is_added_to_the_message()
    {
        var (request, _) = Build(Facts(), extra: "Use different wording.");

        Assert.EndsWith("Use different wording.", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Facts_without_a_final_grade_have_no_data()
    {
        Assert.False(Facts(final: null).HasData);
        Assert.True(Facts().HasData);
    }

    // ---- which answers are accepted ----

    [Fact]
    public void A_good_answer_becomes_the_text_with_the_real_name_back_in()
    {
        var (_, aliases) = Build(Facts());

        var body = AchievementPrompt.ParseBody(Answer(), aliases);

        Assert.True(body.IsSuccess);
        Assert.Equal("Rina Putri has shown a good grasp of fractions. Rina Putri will benefit from more practice with word problems.", body.Value);
    }

    [Fact]
    public void An_answer_wrapped_in_a_code_fence_or_chatter_is_still_read()
    {
        var (_, aliases) = Build(Facts());

        var body = AchievementPrompt.ParseBody($"Here is the text:\n```json\n{Answer()}\n```", aliases);

        Assert.True(body.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no json at all")]
    [InlineData("{ not json }")]
    [InlineData("{\"achieved\":\"Only one part.\"}")]
    [InlineData("{\"achieved\":\"\",\"needsSupport\":\"Something.\"}")]
    public void An_answer_that_is_not_the_expected_json_is_refused(string answer)
    {
        var (_, aliases) = Build(Facts());

        var body = AchievementPrompt.ParseBody(answer, aliases);

        Assert.False(body.IsSuccess);
        Assert.Equal("ai.invalid_output", body.Error.Code);
    }

    [Theory]
    [InlineData("S1 is lazy in class.")]
    [InlineData("S1 is a troublemaker.")]
    [InlineData("S1 anak malas.")]
    public void A_text_that_labels_the_student_negatively_is_refused(string label)
    {
        var (_, aliases) = Build(Facts());

        var body = AchievementPrompt.ParseBody(Answer(needsSupport: label), aliases);

        Assert.False(body.IsSuccess);
        Assert.Equal("ai.invalid_output", body.Error.Code);
    }

    [Fact]
    public void A_part_that_is_too_long_is_refused()
    {
        var (_, aliases) = Build(Facts());

        var body = AchievementPrompt.ParseBody(Answer(achieved: new string('a', AchievementDraftValidator.PartMaxLength + 1)), aliases);

        Assert.False(body.IsSuccess);
    }

    // ---- texts must not be word for word the same ----

    [Fact]
    public void Two_texts_that_differ_only_by_the_name_have_the_same_fingerprint()
    {
        var first = AchievementPrompt.Fingerprint("Rina Putri has shown a good grasp of fractions.", "Rina Putri");
        var second = AchievementPrompt.Fingerprint("Budi Santoso has shown a good grasp of fractions.", "Budi Santoso");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Two_texts_with_different_wording_have_different_fingerprints()
    {
        var first = AchievementPrompt.Fingerprint("Rina Putri has shown a good grasp of fractions.", "Rina Putri");
        var second = AchievementPrompt.Fingerprint("Budi Santoso reached the expected level in fractions.", "Budi Santoso");

        Assert.NotEqual(first, second);
    }
}
