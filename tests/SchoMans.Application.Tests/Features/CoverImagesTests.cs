using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files;

namespace SchoMans.Application.Tests.Features;

public class CoverImagesTests
{
    private static readonly Guid EntityId = Guid.NewGuid();

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public List<string> Deleted { get; } = [];

        public Task<PresignedUpload> CreateUploadUrlAsync(FileBucket bucket, string key, string contentType, TimeSpan lifetime, CancellationToken ct) =>
            throw new NotSupportedException();

        public string CreateDownloadUrl(string key, TimeSpan lifetime) => $"https://private/{key}";

        public string GetPublicUrl(string key) => $"https://public/{key}";

        public Task<StoredObject?> GetObjectInfoAsync(FileBucket bucket, string key, CancellationToken ct) =>
            Task.FromResult(Objects.TryGetValue(key, out var info) ? info : null);

        public Task DeleteAsync(FileBucket bucket, string key, CancellationToken ct)
        {
            Deleted.Add(key);
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("image/png", "png")]
    [InlineData("IMAGE/WEBP", "webp")]
    public void A_news_key_is_module_entity_and_a_fresh_file_name(string contentType, string extension)
    {
        var key = CoverImages.NewKey(CoverImages.NewsModule, EntityId, contentType, "2026-2027");

        var parts = key.Split('/');
        Assert.Equal(["news", EntityId.ToString()], parts[..2]);
        Assert.EndsWith("." + extension, parts[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_announcement_key_also_carries_the_academic_year()
    {
        var key = CoverImages.NewKey(CoverImages.AnnouncementsModule, EntityId, "image/png", "2026-2027");

        Assert.StartsWith($"announcements/2026-2027/{EntityId}/", key, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_keys_for_the_same_entity_are_different()
    {
        Assert.NotEqual(
            CoverImages.NewKey(CoverImages.NewsModule, EntityId, "image/png", "y"),
            CoverImages.NewKey(CoverImages.NewsModule, EntityId, "image/png", "y"));
    }

    [Fact]
    public void News_images_go_to_the_public_bucket_and_announcement_images_to_the_private_one()
    {
        Assert.Equal(FileBucket.Public, CoverImages.BucketFor(CoverImages.NewsModule));
        Assert.Equal(FileBucket.Private, CoverImages.BucketFor(CoverImages.AnnouncementsModule));
    }

    [Fact]
    public async Task No_key_means_no_cover_and_nothing_to_check()
    {
        Assert.Null(await CoverImages.VerifyAsync(new FakeStorage(), CoverImages.NewsModule, EntityId, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("news/not-this-entity/file.png")]
    [InlineData("announcements/2026-2027/{id}/file.png")]
    [InlineData("file.png")]
    public async Task A_key_of_another_module_or_entity_is_rejected(string key)
    {
        var storage = new FakeStorage();
        var resolved = key.Replace("{id}", EntityId.ToString(), StringComparison.Ordinal);
        storage.Objects[resolved] = new StoredObject(100, "image/png");

        var error = await CoverImages.VerifyAsync(storage, CoverImages.NewsModule, EntityId, resolved, CancellationToken.None);

        Assert.Equal("files.invalid_key", error?.Code);
    }

    [Fact]
    public async Task A_key_whose_file_was_never_uploaded_is_rejected()
    {
        var error = await CoverImages.VerifyAsync(new FakeStorage(), CoverImages.NewsModule, EntityId, $"news/{EntityId}/x.png", CancellationToken.None);

        Assert.Equal("files.not_uploaded", error?.Code);
    }

    [Theory]
    [InlineData(6 * 1024 * 1024L, "image/png")]
    [InlineData(100L, "application/pdf")]
    [InlineData(100L, null)]
    public async Task A_file_that_is_too_big_or_not_an_image_is_rejected_and_removed(long size, string? contentType)
    {
        var storage = new FakeStorage();
        var key = $"news/{EntityId}/x.png";
        storage.Objects[key] = new StoredObject(size, contentType);

        var error = await CoverImages.VerifyAsync(storage, CoverImages.NewsModule, EntityId, key, CancellationToken.None);

        Assert.Equal("files.invalid_image", error?.Code);
        Assert.Equal([key], storage.Deleted);
    }

    [Fact]
    public async Task A_small_uploaded_image_of_this_entity_is_accepted()
    {
        var storage = new FakeStorage();
        var key = $"news/{EntityId}/x.png";
        storage.Objects[key] = new StoredObject(CoverImageRules.MaxBytes, "image/png");

        Assert.Null(await CoverImages.VerifyAsync(storage, CoverImages.NewsModule, EntityId, key, CancellationToken.None));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public void Urls_follow_the_bucket_of_the_module()
    {
        var storage = new FakeStorage();

        Assert.Equal("https://public/news/a.png", CoverImages.UrlFor(storage, CoverImages.NewsModule, "news/a.png"));
        Assert.Equal("https://private/announcements/a.png", CoverImages.UrlFor(storage, CoverImages.AnnouncementsModule, "announcements/a.png"));
        Assert.Null(CoverImages.UrlFor(storage, CoverImages.NewsModule, null));
    }
}
