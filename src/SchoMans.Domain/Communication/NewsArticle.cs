using SchoMans.Domain.Common;

namespace SchoMans.Domain.Communication;

/// <summary>Public news shown on the landing page. The body is HTML that the application has already sanitized.</summary>
public sealed class NewsArticle : BaseEntity
{
    private NewsArticle()
    {
    }

    public string Title { get; private set; } = string.Empty;

    /// <summary>The address of the article. Fixed at creation so shared links keep working when the title changes.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? CoverImageKey { get; private set; }

    public NewsCategory Category { get; private set; }

    public string? MetaDescription { get; private set; }

    public PublishStatus Status { get; private set; } = PublishStatus.Draft;

    public DateTimeOffset? PublishedAt { get; private set; }

    public static NewsArticle Create(string title, string slug, string body, NewsCategory category, string? metaDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var article = new NewsArticle { Slug = slug.Trim() };
        article.Update(title, body, category, metaDescription);
        return article;
    }

    public void Update(string title, string body, NewsCategory category, string? metaDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(body);

        Title = title.Trim();
        Body = body;
        Category = category;
        MetaDescription = string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription.Trim();
    }

    public void SetCover(string? coverImageKey) =>
        CoverImageKey = string.IsNullOrWhiteSpace(coverImageKey) ? null : coverImageKey;

    /// <summary>Publishes a draft. Returns false when the article is already published.</summary>
    public bool Publish(DateTimeOffset now)
    {
        if (Status == PublishStatus.Published)
        {
            return false;
        }

        Status = PublishStatus.Published;
        PublishedAt ??= now; // the first publication date stays when an article goes back to draft and out again
        return true;
    }

    /// <summary>Takes a published article back to draft. Returns false when it is already a draft.</summary>
    public bool Unpublish()
    {
        if (Status == PublishStatus.Draft)
        {
            return false;
        }

        Status = PublishStatus.Draft;
        return true;
    }
}
