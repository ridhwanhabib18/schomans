namespace SchoMans.Application.Features.GradeWeights.SetGradeWeights;

/// <param name="Weights">All four components, adding up to 100.</param>
public sealed record SetGradeWeightsCommand(Guid SubjectId, List<GradeWeightItem> Weights);
