using SchoMans.Application.Features.Grading.GetGradeBook;

namespace SchoMans.Application.Features.ReportCards;

public sealed record AchievementDescriptionDto(Guid StudentId, Guid SubjectId, Guid TermId, string Body);

public sealed record ReportCardUrlDto(string Url, DateTimeOffset ExpiresAt);

/// <summary>One subject of a student's published grades.</summary>
public sealed record SubjectGradeDto(
    Guid SubjectId,
    string SubjectName,
    decimal MinimumPassingScore,
    List<ComponentScoreDto> Components,
    decimal? FinalGrade,
    string? Band,
    bool? IsPassing,
    string? Description);

public sealed record StudentGradesDto(Guid StudentId, Guid TermId, DateTimeOffset PublishedAt, bool HasReportCard, List<SubjectGradeDto> Subjects);
