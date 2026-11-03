namespace SchoMans.Application.Features.ProgressSummaries.RegenerateProgressSummary;

/// <summary>Asks the AI for a new draft of one student's summary (also the way to get one for a student the monthly job skipped).</summary>
public sealed record RegenerateProgressSummaryCommand(Guid StudentId, string Month);
