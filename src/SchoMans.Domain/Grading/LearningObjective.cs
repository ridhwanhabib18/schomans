using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>A goal of a subject (Tujuan pembelajaran) that scores can be linked to. The achievement description of the report card speaks about them.</summary>
public sealed class LearningObjective : BaseEntity
{
    public const int CodeMaxLength = 20;
    public const int DescriptionMaxLength = 300;

    private LearningObjective()
    {
    }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static LearningObjective Create(Guid subjectId, string code, string description)
    {
        var objective = new LearningObjective { SubjectId = subjectId };
        objective.Update(code, description);
        return objective;
    }

    public void Update(string code, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (code.Trim().Length > CodeMaxLength)
        {
            throw new ArgumentException($"A code is at most {CodeMaxLength} characters.", nameof(code));
        }

        if (description.Trim().Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"A description is at most {DescriptionMaxLength} characters.", nameof(description));
        }

        Code = code.Trim();
        Description = description.Trim();
    }
}
