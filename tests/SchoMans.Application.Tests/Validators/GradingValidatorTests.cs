using SchoMans.Application.Features.GradeBands.ReplaceGradeBands;
using SchoMans.Application.Features.GradeWeights;
using SchoMans.Application.Features.GradeWeights.SetGradeWeights;
using SchoMans.Application.Features.Grading.EnterGrade;
using SchoMans.Application.Features.ReportCards.WriteAchievementDescription;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Tests.Validators;

/// <summary>The request rules of Phase 4: grade bands, grade weights, entering grades, achievement descriptions.</summary>
public class GradingValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static ReplaceGradeBandsCommand Bands(params (string Label, decimal Min)[] bands) =>
        new(bands.Select(b => new GradeBandInput(b.Label, b.Min)).ToList());

    private static SetGradeWeightsCommand Weights(decimal assignment, decimal quiz, decimal midterm, decimal final) =>
        new(Id, [new(GradeComponent.Assignment, assignment), new(GradeComponent.Quiz, quiz), new(GradeComponent.Midterm, midterm), new(GradeComponent.Final, final)]);

    [Fact]
    public void A_band_scale_needs_unique_labels_and_minimums_and_a_band_at_zero()
    {
        var validator = new ReplaceGradeBandsValidator();

        Assert.True(validator.Validate(Bands(("A", 90), ("B", 60), ("C", 0))).IsValid);
        Assert.False(validator.Validate(Bands(("A", 90), ("B", 60))).IsValid); // nothing starts at 0
        Assert.False(validator.Validate(Bands(("A", 90), ("a", 0))).IsValid); // labels clash, ignoring case
        Assert.False(validator.Validate(Bands(("A", 0), ("B", 0))).IsValid); // minimums clash
        Assert.False(validator.Validate(Bands()).IsValid);
        Assert.False(validator.Validate(Bands(("", 0))).IsValid);
        Assert.False(validator.Validate(Bands(("A", 101), ("B", 0))).IsValid);
    }

    [Fact]
    public void A_band_scale_has_at_most_ten_bands()
    {
        var validator = new ReplaceGradeBandsValidator();
        var many = Enumerable.Range(0, ReplaceGradeBandsValidator.MaxBands + 1).Select(i => ($"L{i}", (decimal)i)).ToArray();

        Assert.False(validator.Validate(Bands(many)).IsValid);
        Assert.True(validator.Validate(Bands(many.Take(ReplaceGradeBandsValidator.MaxBands).ToArray())).IsValid);
    }

    [Fact]
    public void Weights_need_all_four_components_once_and_add_up_to_100()
    {
        var validator = new SetGradeWeightsValidator();

        Assert.True(validator.Validate(Weights(30, 20, 20, 30)).IsValid);
        Assert.True(validator.Validate(Weights(100, 0, 0, 0)).IsValid);
        Assert.False(validator.Validate(Weights(30, 20, 20, 20)).IsValid);
        Assert.False(validator.Validate(Weights(40, 20, 20, 30)).IsValid);
        Assert.False(validator.Validate(Weights(-10, 40, 40, 30)).IsValid);
        Assert.False(validator.Validate(new SetGradeWeightsCommand(Id, [new(GradeComponent.Final, 100)])).IsValid);
        Assert.False(validator.Validate(new SetGradeWeightsCommand(Id, [
            new(GradeComponent.Final, 25), new(GradeComponent.Final, 25), new(GradeComponent.Quiz, 25), new(GradeComponent.Midterm, 25)])).IsValid);
    }

    [Fact]
    public void A_manual_grade_is_a_quiz_midterm_or_final_score_between_0_and_100_with_two_decimals()
    {
        var validator = new EnterGradeValidator();

        Assert.True(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Midterm, 87.5m)).IsValid);
        Assert.True(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Quiz, 0)).IsValid);
        Assert.False(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Assignment, 80)).IsValid); // comes from submissions
        Assert.False(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Final, 100.5m)).IsValid);
        Assert.False(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Final, -1)).IsValid);
        Assert.False(validator.Validate(new EnterGradeCommand(Id, Id, Id, GradeComponent.Final, 80.123m)).IsValid);
        Assert.False(validator.Validate(new EnterGradeCommand(Id, Id, Id, (GradeComponent)9, 80)).IsValid);
    }

    [Fact]
    public void An_achievement_description_is_not_empty_and_not_too_long()
    {
        var validator = new WriteAchievementDescriptionValidator();

        Assert.True(validator.Validate(new WriteAchievementDescriptionCommand(Id, Id, Id, "Good progress.")).IsValid);
        Assert.False(validator.Validate(new WriteAchievementDescriptionCommand(Id, Id, Id, "")).IsValid);
        Assert.False(validator.Validate(new WriteAchievementDescriptionCommand(Id, Id, Id, new string('x', AchievementDescription.BodyMaxLength + 1))).IsValid);
    }
}
