using SchoMans.Application.Features.GradeWeights;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.GetGradeBook;

/// <param name="LearningObjectiveId">When given, the component columns show only the scores linked to that learning objective; the final grade always counts every score.</param>
public sealed record GetGradeBookQuery(Guid ClassroomId, Guid SubjectId, Guid TermId, Guid? LearningObjectiveId = null);

public sealed record GradeBookDto(
    Guid ClassroomId,
    Guid SubjectId,
    Guid TermId,
    decimal MinimumPassingScore,
    bool IsPublished,
    GradeWeightsDto Weights,
    List<GradeBookRowDto> Rows);

/// <param name="FinalGrade">Null while the student has no score at all.</param>
/// <param name="Band">The letter of the final grade; null without a final grade.</param>
/// <param name="Description">The achievement description of the report card, if any (a draft of the AI too).</param>
/// <param name="DescriptionStatus">Draft or Accepted; null without a description. The report card shows only accepted ones.</param>
public sealed record GradeBookRowDto(
    Guid StudentId,
    string StudentName,
    string Nisn,
    List<ComponentScoreDto> Components,
    decimal? FinalGrade,
    string? Band,
    bool? IsPassing,
    string? Description,
    AchievementStatus? DescriptionStatus = null,
    bool DescriptionIsAiDrafted = false);

/// <param name="Average">Null when the student has no score in this component yet.</param>
public sealed record ComponentScoreDto(GradeComponent Component, decimal? Average, int Count);
