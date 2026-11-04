namespace SchoMans.Application.Features.LearningObjectives.UpdateLearningObjective;

/// <param name="Id">Taken from the route.</param>
public sealed record UpdateLearningObjectiveCommand(Guid Id, string Code, string Description);
