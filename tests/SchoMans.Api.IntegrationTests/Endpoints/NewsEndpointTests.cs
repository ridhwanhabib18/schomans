using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Application.Features.News;
using SchoMans.Application.Features.News.CreateNews;
using SchoMans.Application.Features.News.UpdateNews;
using SchoMans.Domain.Communication;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class NewsEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/news";

    private static string UniqueTitle(string prefix = "Article") => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<NewsDto> CreateAsync(HttpClient editor, string? title = null, string body = "<p>Hello</p>", NewsCategory category = NewsCategory.News, string? meta = null)
    {
        var response = await editor.PostJsonAsync(Url, new CreateNewsCommand(title ?? UniqueTitle(), body, category, meta));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<NewsDto>();
    }

    private static UpdateNewsCommand Update(NewsDto article, string? title = null, string? body = null, string? cover = null, string? meta = null) =>
        new(article.Id, title ?? article.Title, body ?? article.Body, Enum.Parse<NewsCategory>(article.Category), meta ?? article.MetaDescription, cover ?? article.CoverImageKey, article.RowVersion);

    // ---- creating ----

    [Fact]
    public async Task An_editor_creates_a_draft_with_an_address_made_from_the_title()
    {
        using var admin = fixture.AsAdmin();
        var title = $"Open House {Guid.NewGuid():N}";

        var article = await CreateAsync(admin, title);

        Assert.Equal("Draft", article.Status);
        Assert.Null(article.PublishedAt);
        Assert.Equal(SlugGeneratorFor(title), article.Slug);
    }

    private static string SlugGeneratorFor(string title) => SchoMans.Domain.Communication.SlugGenerator.Slugify(title);

    [Fact]
    public async Task The_principal_is_an_editor_too()
    {
        using var principal = fixture.AsPrincipal();

        var article = await CreateAsync(principal);

        Assert.Equal("Draft", article.Status);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Other_roles_may_not_manage_news(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new CreateNewsCommand("T", "<p>x</p>", NewsCategory.News, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/{Guid.NewGuid()}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Managing_news_without_a_token_is_unauthorized()
    {
        using var client = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync(Url, new CreateNewsCommand("T", "<p>x</p>", NewsCategory.News, null))).StatusCode);
    }

    [Fact]
    public async Task An_invalid_article_is_rejected()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateNewsCommand("", "", NewsCategory.News, new string('x', 301)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metaDescription", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Two_articles_with_the_same_title_get_different_addresses()
    {
        using var admin = fixture.AsAdmin();
        var title = UniqueTitle("Same Title");

        var first = await CreateAsync(admin, title);
        var second = await CreateAsync(admin, title);
        var third = await CreateAsync(admin, title);

        Assert.Equal(SlugGeneratorFor(title), first.Slug);
        Assert.Equal($"{first.Slug}-2", second.Slug);
        Assert.Equal($"{first.Slug}-3", third.Slug);
    }

    [Fact]
    public async Task The_address_does_not_change_when_the_title_changes()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);

        var response = await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, title: "A Completely New Title"));

        var updated = await response.ReadAsync<NewsDto>();
        Assert.Equal("A Completely New Title", updated.Title);
        Assert.Equal(article.Slug, updated.Slug);
    }

    [Fact]
    public async Task The_address_of_a_deleted_article_is_never_given_to_another_one()
    {
        using var admin = fixture.AsAdmin();
        var title = UniqueTitle("Reused Title");
        var original = await CreateAsync(admin, title);
        await admin.DeleteAsync($"{Url}/{original.Id}");

        var again = await CreateAsync(admin, title);

        Assert.NotEqual(original.Slug, again.Slug);
        Assert.Equal($"{original.Slug}-2", again.Slug);
    }

    // ---- sanitizing ----

    [Fact]
    public async Task Dangerous_html_is_removed_before_it_is_stored_and_before_it_is_shown_to_the_public()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        const string evil =
            "<p onclick=\"steal()\">Hello <strong>world</strong></p>"
            + "<script>alert('xss')</script>"
            + "<img src=x onerror=alert(1)>"
            + "<a href=\"javascript:alert(1)\">bad link</a>"
            + "<a href=\"https://example.com\" target=\"_blank\">good link</a>"
            + "<iframe src=\"https://evil.example\"></iframe>"
            + "<p style=\"position:fixed\">styled</p>";

        var article = await CreateAsync(admin, body: evil);
        await admin.PostAsync($"{Url}/{article.Id}/publish", null);
        var publicArticle = await (await guest.GetAsync($"{Url}/public/{article.Slug}")).ReadAsync<PublicNewsDto>();

        foreach (var html in new[] { article.Body, publicArticle.Body })
        {
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<strong>world</strong>", html, StringComparison.Ordinal);
            Assert.Contains("https://example.com", html, StringComparison.Ordinal);
            Assert.Contains("noopener", html, StringComparison.Ordinal);
        }
    }

    // ---- publishing ----

    [Fact]
    public async Task A_draft_is_invisible_to_the_public_until_it_is_published()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var article = await CreateAsync(admin);

        var beforePublishing = await guest.GetAsync($"{Url}/public/{article.Slug}");
        var published = await admin.PostAsync($"{Url}/{article.Id}/publish", null);
        var afterPublishing = await guest.GetAsync($"{Url}/public/{article.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, beforePublishing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var shown = await published.ReadAsync<NewsDto>();
        Assert.Equal("Published", shown.Status);
        Assert.NotNull(shown.PublishedAt);
        Assert.Equal(HttpStatusCode.OK, afterPublishing.StatusCode);
    }

    [Fact]
    public async Task Publishing_twice_and_unpublishing_a_draft_are_conflicts()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);

        var notPublished = await admin.PostAsync($"{Url}/{article.Id}/unpublish", null);
        await admin.PostAsync($"{Url}/{article.Id}/publish", null);
        var again = await admin.PostAsync($"{Url}/{article.Id}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, notPublished.StatusCode);
        Assert.Equal("news.not_published", await notPublished.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("news.already_published", await again.ProblemCodeAsync());
    }

    [Fact]
    public async Task Unpublishing_hides_the_article_again_and_republishing_keeps_the_first_date()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var article = await CreateAsync(admin);
        var first = await (await admin.PostAsync($"{Url}/{article.Id}/publish", null)).ReadAsync<NewsDto>();

        var unpublished = await admin.PostAsync($"{Url}/{article.Id}/unpublish", null);
        var hidden = await guest.GetAsync($"{Url}/public/{article.Slug}");
        var second = await (await admin.PostAsync($"{Url}/{article.Id}/publish", null)).ReadAsync<NewsDto>();

        Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(first.PublishedAt, second.PublishedAt);
    }

    [Fact]
    public async Task Deleting_an_article_removes_it_from_the_public_site_and_the_editor_list()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var article = await CreateAsync(admin);
        await admin.PostAsync($"{Url}/{article.Id}/publish", null);

        var deleted = await admin.DeleteAsync($"{Url}/{article.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Url}/public/{article.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{article.Id}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_articles_are_not_found_for_every_action()
    {
        using var admin = fixture.AsAdmin();
        var id = Guid.NewGuid();
        var update = new UpdateNewsCommand(id, "T", "<p>x</p>", NewsCategory.News, null, null, "AAAAAAAAB9E=");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutJsonAsync($"{Url}/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{Url}/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{Url}/{id}/unpublish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Url}/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_stale_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, title: "First Editor"))).StatusCode);
        var stale = await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, title: "Second Editor"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    // ---- editors' list ----

    [Fact]
    public async Task The_editors_list_shows_drafts_and_can_be_filtered_by_status_category_and_title()
    {
        using var admin = fixture.AsAdmin();
        var marker = Guid.NewGuid().ToString("N");
        var draft = await CreateAsync(admin, $"Draft {marker}", category: NewsCategory.Award);
        var published = await CreateAsync(admin, $"Published {marker}", category: NewsCategory.News);
        await admin.PostAsync($"{Url}/{published.Id}/publish", null);

        var all = await (await admin.GetAsync($"{Url}?search={marker}")).ReadAsync<PagedResult<NewsDto>>();
        var drafts = await (await admin.GetAsync($"{Url}?search={marker}&status=Draft")).ReadAsync<PagedResult<NewsDto>>();
        var awards = await (await admin.GetAsync($"{Url}?search={marker}&category=Award")).ReadAsync<PagedResult<NewsDto>>();

        Assert.Equal(2, all.TotalCount);
        Assert.Equal([draft.Id], drafts.Items.Select(a => a.Id));
        Assert.Equal([draft.Id], awards.Items.Select(a => a.Id));
    }

    // ---- the public side ----

    [Fact]
    public async Task The_public_list_shows_only_published_articles_newest_first_with_an_excerpt_without_tags()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var older = await CreateAsync(admin, $"Older {marker}", "<p>The <strong>first</strong> article.</p><ul><li>one</li><li>two</li></ul>");
        await admin.PostAsync($"{Url}/{older.Id}/publish", null);
        await Task.Delay(30);
        var newer = await CreateAsync(admin, $"Newer {marker}", "<p>Second.</p>", meta: "A custom description");
        await admin.PostAsync($"{Url}/{newer.Id}/publish", null);
        await CreateAsync(admin, $"Hidden draft {marker}");

        var page = await (await guest.GetAsync($"{Url}/public?pageSize=100")).ReadAsync<PagedResult<PublicNewsSummaryDto>>();
        var mine = page.Items.Where(a => a.Title.EndsWith(marker, StringComparison.Ordinal)).ToList();

        Assert.Equal([newer.Slug, older.Slug], mine.Select(a => a.Slug));
        Assert.Equal("A custom description", mine[0].Excerpt);
        Assert.Equal("The first article. one two", mine[1].Excerpt);
    }

    [Fact]
    public async Task The_public_list_can_be_filtered_by_category_and_is_paged()
    {
        using var guest = fixture.Factory.CreateClient();

        var awards = await (await guest.GetAsync($"{Url}/public?category=Award&pageSize=100")).ReadAsync<PagedResult<PublicNewsSummaryDto>>();
        var firstPage = await (await guest.GetAsync($"{Url}/public?page=1&pageSize=1")).ReadAsync<PagedResult<PublicNewsSummaryDto>>();

        Assert.NotEmpty(awards.Items);
        Assert.All(awards.Items, a => Assert.Equal("Award", a.Category));
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.TotalCount >= 3);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.GetAsync($"{Url}/public?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task A_public_article_carries_a_meta_description_for_search_engines()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var withMeta = await CreateAsync(admin, body: "<p>Body text</p>", meta: "Written by the editor");
        var withoutMeta = await CreateAsync(admin, body: "<p>The start of the article text.</p>");
        await admin.PostAsync($"{Url}/{withMeta.Id}/publish", null);
        await admin.PostAsync($"{Url}/{withoutMeta.Id}/publish", null);

        var first = await (await guest.GetAsync($"{Url}/public/{withMeta.Slug}")).ReadAsync<PublicNewsDto>();
        var second = await (await guest.GetAsync($"{Url}/public/{withoutMeta.Slug}")).ReadAsync<PublicNewsDto>();

        Assert.Equal("Written by the editor", first.MetaDescription);
        Assert.Equal("The start of the article text.", second.MetaDescription);
    }

    [Fact]
    public async Task The_public_side_answers_the_same_for_an_unknown_slug_and_a_draft()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var draft = await CreateAsync(admin);

        var unknown = await guest.GetAsync($"{Url}/public/no-such-article-{Guid.NewGuid():N}");
        var isDraft = await guest.GetAsync($"{Url}/public/{draft.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, isDraft.StatusCode);
        Assert.Equal(await unknown.ProblemCodeAsync(), await isDraft.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_sitemap_lists_published_articles_and_the_public_pages()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var published = await CreateAsync(admin);
        var draft = await CreateAsync(admin);
        await admin.PostAsync($"{Url}/{published.Id}/publish", null);

        var response = await guest.GetAsync("/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", xml, StringComparison.Ordinal);
        Assert.Contains("<loc>https://school.test/</loc>", xml, StringComparison.Ordinal);
        Assert.Contains($"<loc>https://school.test/news/{published.Slug}</loc>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain(draft.Slug, xml, StringComparison.Ordinal);
    }

    // ---- cover images ----

    [Fact]
    public async Task A_cover_image_is_uploaded_with_a_presigned_url_then_saved_and_shown_publicly()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var article = await CreateAsync(admin);

        var issued = await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", article.Id, "image/png", 2048));
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var upload = await issued.ReadAsync<UploadUrlDto>();
        Assert.StartsWith($"news/{article.Id}/", upload.Key, StringComparison.Ordinal);
        Assert.EndsWith(".png", upload.Key, StringComparison.Ordinal);
        Assert.Equal("image/png", upload.ContentType);
        Assert.Contains(upload.Key, upload.Url, StringComparison.Ordinal);

        // What the browser does with the presigned URL.
        fixture.Storage.Put(FileBucket.Public, upload.Key, 2048, "image/png");
        var saved = await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, cover: upload.Key));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var dto = await saved.ReadAsync<NewsDto>();
        Assert.Equal(upload.Key, dto.CoverImageKey);
        Assert.Equal($"https://storage.test/public/{upload.Key}", dto.CoverImageUrl);

        await admin.PostAsync($"{Url}/{article.Id}/publish", null);
        var publicArticle = await (await guest.GetAsync($"{Url}/public/{article.Slug}")).ReadAsync<PublicNewsDto>();
        Assert.Equal(dto.CoverImageUrl, publicArticle.CoverImageUrl);
    }

    [Fact]
    public async Task Replacing_the_cover_removes_the_old_file()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);
        var first = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", article.Id, "image/png", 100))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Public, first.Key);
        var withFirst = await (await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, cover: first.Key))).ReadAsync<NewsDto>();
        var second = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", article.Id, "image/jpeg", 100))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Public, second.Key, 100, "image/jpeg");

        var replaced = await admin.PutJsonAsync($"{Url}/{article.Id}", new UpdateNewsCommand(
            article.Id, article.Title, article.Body, NewsCategory.News, null, second.Key, withFirst.RowVersion));

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.False(fixture.Storage.Contains(FileBucket.Public, first.Key));
        Assert.True(fixture.Storage.Contains(FileBucket.Public, second.Key));
    }

    [Fact]
    public async Task A_cover_key_whose_file_was_never_uploaded_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);
        var upload = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", article.Id, "image/png", 100))).ReadAsync<UploadUrlDto>();

        var response = await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, cover: upload.Key)); // nothing was uploaded

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("files.not_uploaded", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_cover_key_of_another_article_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var mine = await CreateAsync(admin);
        var other = await CreateAsync(admin);
        var theirs = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", other.Id, "image/png", 100))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Public, theirs.Key);

        var response = await admin.PutJsonAsync($"{Url}/{mine.Id}", Update(mine, cover: theirs.Key));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("files.invalid_key", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_file_that_turns_out_to_be_too_big_is_rejected_and_deleted()
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);
        var upload = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", article.Id, "image/png", 100))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Public, upload.Key, sizeBytes: 50 * 1024 * 1024); // the browser lied about the size

        var response = await admin.PutJsonAsync($"{Url}/{article.Id}", Update(article, cover: upload.Key));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("files.invalid_image", await response.ProblemCodeAsync());
        Assert.False(fixture.Storage.Contains(FileBucket.Public, upload.Key));
    }

    [Theory]
    [InlineData("news", "application/pdf", 1000)]
    [InlineData("news", "image/png", 6 * 1024 * 1024)]
    [InlineData("news", "image/png", 0)]
    [InlineData("videos", "image/png", 1000)]
    public async Task An_upload_url_is_refused_for_the_wrong_type_size_or_module(string module, string type, long size)
    {
        using var admin = fixture.AsAdmin();
        var article = await CreateAsync(admin);

        var response = await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand(module, article.Id, type, size));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_upload_url_needs_a_saved_record()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", Guid.NewGuid(), "image/png", 100));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("files.entity_not_found", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    public async Task Only_editors_get_upload_urls(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("news", Guid.NewGuid(), "image/png", 100));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
