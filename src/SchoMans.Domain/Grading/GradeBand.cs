using SchoMans.Domain.Common;

namespace SchoMans.Domain.Grading;

/// <summary>A letter band of the school: a final grade from <see cref="MinScore"/> up gets <see cref="Label"/> (for example A from 90).</summary>
public sealed class GradeBand : BaseEntity
{
    public const int LabelMaxLength = 10;

    private GradeBand()
    {
    }

    public string Label { get; private set; } = string.Empty;

    public decimal MinScore { get; private set; }

    public static GradeBand Create(string label, decimal minScore)
    {
        var band = new GradeBand();
        band.Update(label, minScore);
        return band;
    }

    public void Update(string label, decimal minScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        if (label.Trim().Length > LabelMaxLength)
        {
            throw new ArgumentException($"A label is at most {LabelMaxLength} characters.", nameof(label));
        }

        if (minScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minScore), "A minimum score is between 0 and 100.");
        }

        Label = label.Trim();
        MinScore = minScore;
    }

    /// <summary>The band of a final grade: the one with the highest minimum that the grade reaches. Null when the bands do not cover it.</summary>
    public static string? LabelFor(decimal finalGrade, IEnumerable<GradeBand> bands) =>
        bands.Where(b => finalGrade >= b.MinScore).OrderByDescending(b => b.MinScore).Select(b => b.Label).FirstOrDefault();
}
