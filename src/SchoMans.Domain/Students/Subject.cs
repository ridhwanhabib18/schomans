using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

public sealed class Subject : BaseEntity
{
    private Subject()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>KKM: the score a student needs to pass this subject (0 to 100).</summary>
    public decimal MinimumPassingScore { get; private set; }

    public static Subject Create(string name, decimal minimumPassingScore)
    {
        var subject = new Subject();
        subject.Update(name, minimumPassingScore);
        return subject;
    }

    public void Update(string name, decimal minimumPassingScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (minimumPassingScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPassingScore), "The minimum passing score is between 0 and 100.");
        }

        Name = name.Trim();
        MinimumPassingScore = minimumPassingScore;
    }
}
