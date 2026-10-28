namespace SchoMans.Domain.Grading;

/// <summary>Turns the scores of a student in a subject into the final grade.</summary>
public static class GradeCalculator
{
    public const decimal TotalWeight = 100m;

    public static readonly IReadOnlyDictionary<GradeComponent, decimal> DefaultWeights = new Dictionary<GradeComponent, decimal>
    {
        [GradeComponent.Assignment] = 30m,
        [GradeComponent.Quiz] = 20m,
        [GradeComponent.Midterm] = 20m,
        [GradeComponent.Final] = 30m,
    };

    /// <summary>
    /// The average of each component, weighted. A component without scores is left out and its weight is shared over the
    /// others in proportion. Null when no component has a score (or the weights of the scored ones are all zero).
    /// </summary>
    public static decimal? FinalGrade(IReadOnlyDictionary<GradeComponent, decimal> weights, IEnumerable<(GradeComponent Component, decimal Score)> scores)
    {
        var averages = scores
            .GroupBy(s => s.Component)
            .ToDictionary(g => g.Key, g => g.Average(s => s.Score));

        var weightSum = 0m;
        var weighted = 0m;
        foreach (var (component, average) in averages)
        {
            var weight = weights.GetValueOrDefault(component);
            weightSum += weight;
            weighted += average * weight;
        }

        return weightSum == 0 ? null : decimal.Round(weighted / weightSum, 2, MidpointRounding.AwayFromZero);
    }

    public static bool WeightsAreValid(IEnumerable<decimal> weights) => weights.Sum() == TotalWeight;
}
