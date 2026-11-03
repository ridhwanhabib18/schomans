using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.ProgressSummaries;

namespace SchoMans.Application.Tests.Features;

/// <summary>
/// The test set of AI-01 (PRD: at least 10 cases, run whenever the prompt changes): what the model is told, and which
/// answers are accepted. Provider answers are scripted here, so the set runs without a network; the same expectations are
/// what a manual run against Ollama or Claude is judged by.
/// </summary>
public class ProgressSummaryPromptTests
{
    private static readonly Guid StudentId = Guid.NewGuid();

    private static ProgressFacts Facts(
        int present = 18,
        int absent = 0,
        int sick = 0,
        IReadOnlyList<GradeFact>? grades = null,
        int submitted = 3,
        int late = 0,
        int notHandedIn = 0,
        decimal? average = null,
        IReadOnlyList<string>? notes = null) =>
        new("2026-10", present, 0, sick, absent, grades ?? [], submitted, late, notHandedIn, average, notes ?? []);

    private static (AiRequest Request, NameAliases Aliases) Build(ProgressFacts facts, string name = "Rina Putri")
    {
        var aliases = new NameAliases();
        var alias = aliases.For(StudentId, name);
        return (ProgressSummaryPrompt.Build(facts, alias, aliases), aliases);
    }

    private static string Answer(string paragraph = "A steady month.", string goingWell = "Good effort.", string needsAttention = "Nothing stands out.", string homeSuggestion = "Read together.") =>
        System.Text.Json.JsonSerializer.Serialize(new { paragraph, goingWell, needsAttention, homeSuggestion });

    // ---- what the model is told ----

    [Fact]
    public void The_request_is_for_ai_01_and_asks_for_json()
    {
        var (request, _) = Build(Facts());

        Assert.Equal(AiFeatures.ProgressSummary, request.Feature);
        Assert.True(request.JsonOutput);
        Assert.Contains("JSON", request.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_student_name_never_reaches_the_prompt_only_the_code()
    {
        var (request, _) = Build(Facts(notes: ["Rina Putri helped a classmate; rina was calm."]), "Rina Putri");

        Assert.Contains("Student: S1", request.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Rina", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Putri", request.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1 helped a classmate", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_carries_the_facts_of_the_month()
    {
        var facts = Facts(present: 17, absent: 2, sick: 1, grades: [new GradeFact("Mathematics", "Midterm", 82.5m)], submitted: 4, late: 1, notHandedIn: 2, average: 78.5m);

        var (request, _) = Build(facts);

        Assert.Contains("Month: 2026-10", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("present 17", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("absent 2", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Mathematics, Midterm: 82.5", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("handed in 4 (1 of them late), not handed in 2", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("78.5", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_topic_without_facts_is_said_to_have_none_instead_of_left_for_the_model_to_fill()
    {
        var (request, _) = Build(Facts(present: 0, submitted: 0));

        Assert.Contains("no lessons recorded", request.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("none entered this month", request.UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Teacher notes:", request.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rules_forbid_guessing_causes_and_labelling_the_child()
    {
        Assert.Contains("Never guess why", ProgressSummaryPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("never label the child", ProgressSummaryPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Use only the facts", ProgressSummaryPrompt.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Facts_without_any_data_are_recognised_so_no_summary_is_asked_for()
    {
        var empty = new ProgressFacts("2026-10", 0, 0, 0, 0, [], 0, 0, 0, null, []);

        Assert.False(empty.HasData);
        Assert.True(Facts().HasData);
        Assert.True(empty with { Notes = ["A note."] } is { HasData: true });
        Assert.True(empty with { NotHandedIn = 1 } is { HasData: true });
    }

    // ---- which answers are accepted ----

    [Fact]
    public void A_complete_answer_becomes_one_paragraph_and_three_labelled_points()
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody(Answer(), aliases);

        Assert.True(body.IsSuccess);
        Assert.Equal(
            "A steady month.\n\nWhat is going well: Good effort.\nWhat needs attention: Nothing stands out.\nA suggestion for home: Read together.",
            body.Value);
    }

    [Fact]
    public void The_real_name_replaces_the_code_in_the_answer()
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody(Answer(paragraph: "S1 had a good month."), aliases);

        Assert.StartsWith("Rina Putri had a good month.", body.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void An_answer_wrapped_in_a_code_fence_or_chatter_is_still_read()
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody($"Here you go:\n```json\n{Answer()}\n```", aliases);

        Assert.True(body.IsSuccess);
    }

    [Theory]
    [InlineData("This is not JSON at all.")]
    [InlineData("{ broken")]
    [InlineData("{}")]
    [InlineData("""{"paragraph":"Only this."}""")]
    [InlineData("""{"paragraph":"A","goingWell":"B","needsAttention":"","homeSuggestion":"D"}""")]
    [InlineData("""{"paragraph":"A","goingWell":null,"needsAttention":"C","homeSuggestion":"D"}""")]
    public void An_answer_that_is_not_the_agreed_shape_is_refused(string answer)
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody(answer, aliases);

        Assert.True(body.IsFailure);
        Assert.Equal("ai.invalid_output", body.Error.Code);
    }

    [Fact]
    public void A_part_that_is_too_long_is_refused()
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody(Answer(paragraph: new string('x', ProgressSummaryDraftValidator.ParagraphMaxLength + 1)), aliases);

        Assert.Equal("ai.invalid_output", body.Error.Code);
    }

    [Theory]
    [InlineData("S1 is lazy about homework.")]
    [InlineData("A stupid mistake.")]
    [InlineData("S1 is a troublemaker in class.")]
    [InlineData("Anak ini malas.")]
    public void A_text_that_labels_the_child_negatively_is_refused(string needsAttention)
    {
        var (_, aliases) = Build(Facts());

        var body = ProgressSummaryPrompt.ParseBody(Answer(needsAttention: needsAttention), aliases);

        Assert.Equal("ai.invalid_output", body.Error.Code);
    }

    [Fact]
    public void Words_that_only_contain_a_label_are_not_mistaken_for_it()
    {
        var (_, aliases) = Build(Facts());

        // "lazy" inside "Blazy" or "dumbbell" is not the word.
        var body = ProgressSummaryPrompt.ParseBody(Answer(goingWell: "Enjoyed the dumbbell exercise in sports."), aliases);

        Assert.True(body.IsSuccess);
    }
}
