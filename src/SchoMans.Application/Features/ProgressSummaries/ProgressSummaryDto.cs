using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries;

public sealed record ProgressSummaryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Month,
    string Body,
    string Status,
    bool IsAiDrafted,
    DateTimeOffset? SentAt);

internal static class ProgressSummaryMapping
{
    public static ProgressSummaryDto ToDto(ProgressSummary summary, string studentName) =>
        new(summary.Id, summary.StudentId, studentName, summary.Month, summary.Body, summary.Status.ToString(), summary.IsAiDrafted, summary.SentAt);
}
