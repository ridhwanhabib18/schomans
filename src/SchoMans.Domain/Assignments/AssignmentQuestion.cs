using SchoMans.Domain.Common;

namespace SchoMans.Domain.Assignments;

/// <summary>
/// A question of an online quiz: a question of the bank, its place in the quiz and the points it is worth. The question itself stays in
/// the bank, which is why a question used in a quiz can no longer be changed or deleted.
/// </summary>
public sealed class AssignmentQuestion : BaseEntity
{
    public const int MaxPoints = 100;
    public const int MaxQuestionsPerQuiz = 50;

    private AssignmentQuestion()
    {
    }

    public Guid AssignmentId { get; private set; }

    public Assignment? Assignment { get; private set; }

    public Guid QuestionId { get; private set; }

    public Question? Question { get; private set; }

    /// <summary>The place in the quiz, from 1.</summary>
    public int Order { get; private set; }

    /// <summary>What a right answer earns; every question counts the same (1) unless the teacher changes it.</summary>
    public int Points { get; private set; }

    public static AssignmentQuestion Create(Guid assignmentId, Guid questionId, int order, int points)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(points, MaxPoints);

        return new AssignmentQuestion { AssignmentId = assignmentId, QuestionId = questionId, Order = order, Points = points };
    }
}
