namespace SchoMans.Application.Features.Grading.PublishGrades;

public sealed record PublishGradesCommand(Guid ClassroomId, Guid TermId);

public sealed record GradePublicationDto(Guid ClassroomId, Guid TermId, DateTimeOffset PublishedAt);
