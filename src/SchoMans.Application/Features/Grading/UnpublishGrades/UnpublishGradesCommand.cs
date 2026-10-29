namespace SchoMans.Application.Features.Grading.UnpublishGrades;

public sealed record UnpublishGradesCommand(Guid ClassroomId, Guid TermId);
