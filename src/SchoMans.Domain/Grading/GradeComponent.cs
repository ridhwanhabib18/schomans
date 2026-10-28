namespace SchoMans.Domain.Grading;

/// <summary>The parts a subject grade is made of; each has a weight per subject (Phase 4).</summary>
public enum GradeComponent
{
    Assignment,
    Quiz,
    Midterm,
    Final,
}
