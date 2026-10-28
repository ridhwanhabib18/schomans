namespace SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;

/// <summary>What happens to one classroom of the current year.</summary>
/// <param name="FromClassroomId">A classroom of the current academic year.</param>
/// <param name="Graduate">True when its students leave school instead of moving on.</param>
/// <param name="ToClassroomName">The classroom in the new year the students move to. Several mappings can name the same one.</param>
/// <param name="HomeroomTeacherId">The homeroom teacher of the new classroom (needed for a classroom that is created).</param>
public sealed record RolloverMapping(Guid FromClassroomId, bool Graduate, string? ToClassroomName, Guid? HomeroomTeacherId);

/// <summary>
/// Starts a new academic year and promotes the students in bulk: every classroom of the current year is mapped to a new
/// classroom (created here) or marked as graduating. All of it happens in one transaction.
/// </summary>
public sealed record RolloverAcademicYearCommand(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<TermInput> Terms,
    IReadOnlyList<RolloverMapping> Mappings);

public sealed record RolloverResult(Guid AcademicYearId, int MovedStudents, int GraduatedStudents, int CreatedClassrooms);
