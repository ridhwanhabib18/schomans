namespace SchoMans.Application.Features.ReportCards.WriteAchievementDescription;

public sealed record WriteAchievementDescriptionCommand(Guid StudentId, Guid SubjectId, Guid TermId, string Body);
