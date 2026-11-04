namespace SchoMans.Application.Features.ProgressSummaries.SendProgressSummaries;

/// <summary>Sends every approved summary of the classroom for the month to the parents.</summary>
public sealed record SendProgressSummariesCommand(Guid ClassroomId, string Month);

public sealed record SentProgressSummariesDto(int Sent, int ParentsNotified);
