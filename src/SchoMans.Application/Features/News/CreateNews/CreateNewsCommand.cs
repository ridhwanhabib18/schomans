using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.CreateNews;

/// <summary>Creates a draft. The body is rich-text HTML; it is sanitized before it is stored.</summary>
public sealed record CreateNewsCommand(string Title, string Body, NewsCategory Category, string? MetaDescription);
