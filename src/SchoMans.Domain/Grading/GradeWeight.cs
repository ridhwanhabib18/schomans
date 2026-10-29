using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>How much a component counts in the final grade of a subject, in percent. The weights of a subject add up to 100.</summary>
public sealed class GradeWeight : BaseEntity
{
    private GradeWeight()
    {
    }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public GradeComponent Component { get; private set; }

    public decimal WeightPercent { get; private set; }

    public static GradeWeight Create(Guid subjectId, GradeComponent component, decimal weightPercent)
    {
        var weight = new GradeWeight { SubjectId = subjectId, Component = component };
        weight.Update(weightPercent);
        return weight;
    }

    public void Update(decimal weightPercent)
    {
        if (weightPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(weightPercent), "A weight is between 0 and 100.");
        }

        WeightPercent = weightPercent;
    }
}
