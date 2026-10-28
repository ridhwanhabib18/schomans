using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.ListNews;

/// <summary>The editors' list: drafts and published articles.</summary>
public sealed record ListNewsQuery(int Page = 1, int PageSize = 20, string? Search = null, PublishStatus? Status = null, NewsCategory? Category = null);
