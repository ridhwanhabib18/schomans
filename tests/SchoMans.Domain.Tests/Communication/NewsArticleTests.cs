using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class NewsArticleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static NewsArticle Draft() => NewsArticle.Create("Open house", "open-house", "<p>Come</p>", NewsCategory.AdmissionInfo, " Visit us ");

    [Fact]
    public void A_new_article_is_a_draft_without_a_publication_date()
    {
        var article = Draft();

        Assert.Equal(PublishStatus.Draft, article.Status);
        Assert.Null(article.PublishedAt);
        Assert.Equal("Visit us", article.MetaDescription);
    }

    [Fact]
    public void Publishing_a_draft_sets_the_publication_date_once()
    {
        var article = Draft();

        Assert.True(article.Publish(Now));
        Assert.Equal(PublishStatus.Published, article.Status);
        Assert.Equal(Now, article.PublishedAt);

        Assert.False(article.Publish(Now.AddDays(1)));
        Assert.Equal(Now, article.PublishedAt);
    }

    [Fact]
    public void Unpublishing_returns_to_draft_and_keeps_the_first_publication_date()
    {
        var article = Draft();
        article.Publish(Now);

        Assert.True(article.Unpublish());
        Assert.Equal(PublishStatus.Draft, article.Status);
        Assert.False(article.Unpublish());

        article.Publish(Now.AddDays(5));
        Assert.Equal(Now, article.PublishedAt);
    }

    [Fact]
    public void Changing_the_title_never_changes_the_address()
    {
        var article = Draft();

        article.Update("A completely different title", "<p>New</p>", NewsCategory.News, null);

        Assert.Equal("open-house", article.Slug);
        Assert.Equal("A completely different title", article.Title);
        Assert.Null(article.MetaDescription);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_cover_key_means_no_cover(string? key)
    {
        var article = Draft();
        article.SetCover("news/abc/cover.png");

        article.SetCover(key);

        Assert.Null(article.CoverImageKey);
    }

    [Fact]
    public void Requires_a_title_and_a_slug()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewsArticle.Create(" ", "slug", "<p>x</p>", NewsCategory.News, null));
        Assert.ThrowsAny<ArgumentException>(() => NewsArticle.Create("Title", " ", "<p>x</p>", NewsCategory.News, null));
    }
}
