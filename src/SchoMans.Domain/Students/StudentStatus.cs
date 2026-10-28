namespace SchoMans.Domain.Students;

public enum StudentStatus
{
    /// <summary>Currently enrolled in a classroom of the active academic year.</summary>
    Active,

    /// <summary>Finished school at an academic-year rollover. The record and the last classroom are kept for history.</summary>
    Graduated,
}
