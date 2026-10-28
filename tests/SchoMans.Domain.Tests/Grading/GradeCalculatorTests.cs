using SchoMans.Domain.Grading;

namespace SchoMans.Domain.Tests.Grading;

public class GradeCalculatorTests
{
    private static readonly IReadOnlyDictionary<GradeComponent, decimal> Weights = GradeCalculator.DefaultWeights;

    [Fact]
    public void The_default_weights_add_up_to_100()
    {
        Assert.True(GradeCalculator.WeightsAreValid(Weights.Values));
    }

    [Fact]
    public void The_final_grade_is_the_weighted_average_of_the_component_averages()
    {
        var scores = new (GradeComponent, decimal)[]
        {
            (GradeComponent.Assignment, 80), (GradeComponent.Assignment, 100), // average 90
            (GradeComponent.Quiz, 70),
            (GradeComponent.Midterm, 60),
            (GradeComponent.Final, 80),
        };

        // 90*30 + 70*20 + 60*20 + 80*30 = 2700 + 1400 + 1200 + 2400 = 7700 / 100
        Assert.Equal(77m, GradeCalculator.FinalGrade(Weights, scores));
    }

    [Fact]
    public void A_component_without_scores_shares_its_weight_over_the_others()
    {
        var scores = new (GradeComponent, decimal)[] { (GradeComponent.Assignment, 90), (GradeComponent.Final, 60) };

        // 90*30 + 60*30 = 4500 over a weight of 60
        Assert.Equal(75m, GradeCalculator.FinalGrade(Weights, scores));
    }

    [Fact]
    public void Without_any_score_there_is_no_final_grade()
    {
        Assert.Null(GradeCalculator.FinalGrade(Weights, []));
    }

    [Fact]
    public void Scores_of_components_with_a_zero_weight_do_not_count_and_alone_give_no_grade()
    {
        var weights = new Dictionary<GradeComponent, decimal> { [GradeComponent.Final] = 100 };

        Assert.Null(GradeCalculator.FinalGrade(weights, [(GradeComponent.Quiz, 50)]));
        Assert.Equal(80m, GradeCalculator.FinalGrade(weights, [(GradeComponent.Quiz, 50), (GradeComponent.Final, 80)]));
    }

    [Fact]
    public void The_final_grade_is_rounded_to_two_decimals_away_from_zero()
    {
        var weights = new Dictionary<GradeComponent, decimal> { [GradeComponent.Assignment] = 50, [GradeComponent.Quiz] = 50 };

        Assert.Equal(66.67m, GradeCalculator.FinalGrade(weights, [(GradeComponent.Assignment, 100), (GradeComponent.Quiz, 33.34m)]));
        Assert.Equal(0.01m, GradeCalculator.FinalGrade(weights, [(GradeComponent.Assignment, 0.01m), (GradeComponent.Quiz, 0.01m)]));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(99, false)]
    [InlineData(101, false)]
    public void Weights_must_add_up_to_exactly_100(decimal total, bool valid)
    {
        Assert.Equal(valid, GradeCalculator.WeightsAreValid([total - 50, 50]));
    }
}
