namespace SchoMans.Application.Features.LearningObjectives.CreateLearningObjective;

public sealed record CreateLearningObjectiveCommand(Guid SubjectId, string Code, string Description);
