using SchoMans.Domain.Common;

namespace SchoMans.Domain.Assignments;

/// <summary>
/// The rubric of an assignment (AI-05): the criteria a piece of work is judged on, each with a description and a score range
/// of 0 to its maximum. The criteria are kept as one JSON document that the Application layer reads and checks.
/// </summary>
public sealed class Rubric : BaseEntity
{
    public const int CriteriaJsonMaxLength = 6_000;

    private Rubric()
    {
    }

    public Guid AssignmentId { get; private set; }

    public Assignment? Assignment { get; private set; }

    public string CriteriaJson { get; private set; } = "[]";

    public static Rubric Create(Guid assignmentId, string criteriaJson)
    {
        var rubric = new Rubric { AssignmentId = assignmentId };
        rubric.Replace(criteriaJson);
        return rubric;
    }

    public void Replace(string criteriaJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(criteriaJson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(criteriaJson.Length, CriteriaJsonMaxLength);

        CriteriaJson = criteriaJson;
    }
}
