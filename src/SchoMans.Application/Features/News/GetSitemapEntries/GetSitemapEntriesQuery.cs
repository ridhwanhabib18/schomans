namespace SchoMans.Application.Features.News.GetSitemapEntries;

public sealed record GetSitemapEntriesQuery;

public sealed record SitemapEntry(string Slug, DateTimeOffset LastModified);
