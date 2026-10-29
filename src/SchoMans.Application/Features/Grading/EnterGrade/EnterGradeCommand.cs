using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.EnterGrade;

/// <param name="Component">Quiz, Midterm or Final: assignment scores come from the scored submissions.</param>
/// <param name="Score">0 to 100.</param>
public sealed record EnterGradeCommand(Guid StudentId, Guid SubjectId, Guid TermId, GradeComponent Component, decimal Score);

public sealed record EnteredGradeDto(Guid Id, Guid StudentId, Guid SubjectId, Guid TermId, GradeComponent Component, decimal Score);
