using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.ListPublicNews;

/// <summary>The public news list for guests: published articles only, newest first.</summary>
public sealed record ListPublicNewsQuery(int Page = 1, int PageSize = 10, NewsCategory? Category = null);
