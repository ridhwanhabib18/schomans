using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeWeights;

/// <param name="IsDefault">True while the subject has no weights of its own and the default 30/20/20/30 applies.</param>
public sealed record GradeWeightsDto(Guid SubjectId, List<GradeWeightItem> Weights, bool IsDefault);

public sealed record GradeWeightItem(GradeComponent Component, decimal WeightPercent);
