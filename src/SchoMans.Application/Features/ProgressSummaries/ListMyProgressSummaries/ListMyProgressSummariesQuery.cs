namespace SchoMans.Application.Features.ProgressSummaries.ListMyProgressSummaries;

/// <param name="StudentId">One child; all the children of the parent when left out.</param>
public sealed record ListMyProgressSummariesQuery(Guid? StudentId = null, int Page = 1, int PageSize = 20);
