namespace SchoMans.Application.Features.ProgressSummaries.ListClassroomProgressSummaries;

/// <param name="Month"><c>yyyy-MM</c>; the previous month when left out.</param>
public sealed record ListClassroomProgressSummariesQuery(Guid ClassroomId, string? Month = null);
