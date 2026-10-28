using SchoMans.Application.Features.GradeWeights;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.GetGradeBook;

public sealed record GetGradeBookQuery(Guid ClassroomId, Guid SubjectId, Guid TermId);

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
/// <param name="Description">The achievement description the teacher wrote for the report card, if any.</param>
public sealed record GradeBookRowDto(
    Guid StudentId,
    string StudentName,
    string Nisn,
    List<ComponentScoreDto> Components,
    decimal? FinalGrade,
    string? Band,
    bool? IsPassing,
    string? Description);

/// <param name="Average">Null when the student has no score in this component yet.</param>
public sealed record ComponentScoreDto(GradeComponent Component, decimal? Average, int Count);
