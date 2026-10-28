namespace SchoMans.Application.Features.Subjects.UpdateSubject;

public sealed record UpdateSubjectCommand(Guid Id, string Name, decimal MinimumPassingScore, string RowVersion);
