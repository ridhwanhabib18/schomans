using System.Net;
using System.Net.Http.Headers;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.IntegrationTests.Storage;

/// <summary>
/// The real S3 code against a real SeaweedFS: presigned uploads and downloads, the public and the private bucket,
/// content-type binding and expiry. These are the guarantees the PRD's "S3 storage design" rests on.
/// </summary>
public class S3FileStorageTests(SeaweedFixture seaweed) : IClassFixture<SeaweedFixture>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static string NewKey(string extension = "png") => $"tests/{Guid.NewGuid():N}/image.{extension}";

    private static async Task<HttpResponseMessage> PutAsync(string url, byte[] body, string contentType)
    {
        using var client = new HttpClient();
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await client.PutAsync(url, content);
    }

    private static async Task<HttpResponseMessage> GetAsync(string url)
    {
        using var client = new HttpClient();
        return await client.GetAsync(url);
    }

    [Fact]
    public async Task A_file_uploaded_with_a_presigned_url_can_be_looked_up_with_its_size_and_type()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);

        var response = await PutAsync(upload.Url, Png, "image/png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await seaweed.Storage.GetObjectInfoAsync(FileBucket.Public, key, CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal(Png.Length, info.SizeBytes);
        Assert.Equal("image/png", info.ContentType);
        await seaweed.Storage.DeleteAsync(FileBucket.Public, key, CancellationToken.None);
    }

    [Fact]
    public async Task An_object_that_was_never_uploaded_is_not_found()
    {
        Assert.Null(await seaweed.Storage.GetObjectInfoAsync(FileBucket.Public, NewKey(), CancellationToken.None));
        Assert.Null(await seaweed.Storage.GetObjectInfoAsync(FileBucket.Private, NewKey(), CancellationToken.None));
    }

    [Fact]
    public async Task The_upload_url_only_accepts_the_content_type_it_was_signed_for()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);

        var response = await PutAsync(upload.Url, "<html>not an image</html>"u8.ToArray(), "text/html");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await seaweed.Storage.GetObjectInfoAsync(FileBucket.Public, key, CancellationToken.None));
    }

    [Fact]
    public async Task The_upload_url_only_works_for_its_own_key()
    {
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, NewKey(), "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        var otherKey = NewKey();
        var tampered = upload.Url.Replace(upload.Url[(upload.Url.IndexOf("/tests/", StringComparison.Ordinal) + 1)..upload.Url.IndexOf("/image.png", StringComparison.Ordinal)], otherKey[..otherKey.IndexOf("/image.png", StringComparison.Ordinal)], StringComparison.Ordinal);

        var response = await PutAsync(tampered, Png, "image/png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_upload_url_is_refused()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, key, "image/png", TimeSpan.FromSeconds(1), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));

        var response = await PutAsync(upload.Url, Png, "image/png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await seaweed.Storage.GetObjectInfoAsync(FileBucket.Public, key, CancellationToken.None));
    }

    [Fact]
    public async Task A_public_object_can_be_read_by_anybody_through_its_public_url()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        await PutAsync(upload.Url, Png, "image/png");

        var response = await GetAsync(seaweed.Storage.GetPublicUrl(key));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        await seaweed.Storage.DeleteAsync(FileBucket.Public, key, CancellationToken.None);
    }

    [Fact]
    public async Task A_private_object_is_closed_to_anybody_without_a_presigned_link_and_open_with_one()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Private, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(upload.Url, Png, "image/png")).StatusCode);

        // The address a public URL would have in the private bucket, with no signature: refused.
        var anonymousUrl = $"{seaweed.Options.Endpoint}/{seaweed.Options.PrivateBucket}/{key}";
        var anonymous = await GetAsync(anonymousUrl);
        var presigned = await GetAsync(seaweed.Storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5)));

        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, presigned.StatusCode);
        Assert.Equal(Png, await presigned.Content.ReadAsByteArrayAsync());
        await seaweed.Storage.DeleteAsync(FileBucket.Private, key, CancellationToken.None);
    }

    [Fact]
    public async Task A_download_link_stops_working_when_it_expires()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Private, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        await PutAsync(upload.Url, Png, "image/png");
        var link = seaweed.Storage.CreateDownloadUrl(key, TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(3));

        var response = await GetAsync(link);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await seaweed.Storage.DeleteAsync(FileBucket.Private, key, CancellationToken.None);
    }

    [Fact]
    public async Task A_deleted_object_is_gone()
    {
        var key = NewKey();
        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, key, "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        await PutAsync(upload.Url, Png, "image/png");

        await seaweed.Storage.DeleteAsync(FileBucket.Public, key, CancellationToken.None);

        Assert.Null(await seaweed.Storage.GetObjectInfoAsync(FileBucket.Public, key, CancellationToken.None));
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(seaweed.Storage.GetPublicUrl(key))).StatusCode);
    }

    [Fact]
    public async Task Creating_a_bucket_twice_is_harmless()
    {
        await seaweed.Storage.EnsureBucketAsync(seaweed.Options.PrivateBucket, CancellationToken.None);
        await seaweed.Storage.EnsureBucketAsync(seaweed.Options.PublicBucket, CancellationToken.None);
    }

    [Fact]
    public async Task Upload_urls_are_signed_for_the_address_the_browser_uses_not_the_one_the_api_uses()
    {
        using var storage = seaweed.CreateStorage(options => options.BrowserEndpoint = "http://storage.school.example:9000");

        var upload = await storage.CreateUploadUrlAsync(FileBucket.Public, "news/a/b.png", "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.StartsWith("http://storage.school.example:9000/schomans-public/news/a/b.png?", upload.Url, StringComparison.Ordinal);
        Assert.StartsWith("http://storage.school.example:9000/schomans-private/", storage.CreateDownloadUrl("x/y.png", TimeSpan.FromMinutes(1)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Presigned_urls_follow_the_scheme_of_the_endpoint()
    {
        using var secure = seaweed.CreateStorage(options => options.BrowserEndpoint = "https://account.r2.example.com");
        using var plain = seaweed.CreateStorage(options => options.BrowserEndpoint = "http://localhost:8333");

        var https = await secure.CreateUploadUrlAsync(FileBucket.Public, "k.png", "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);
        var http = await plain.CreateUploadUrlAsync(FileBucket.Public, "k.png", "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.StartsWith("https://", https.Url, StringComparison.Ordinal);
        Assert.StartsWith("http://", http.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void The_public_url_is_the_base_address_plus_the_key_without_doubled_slashes()
    {
        using var storage = seaweed.CreateStorage(options => options.PublicBaseUrl = "https://cdn.school.example/");

        Assert.Equal("https://cdn.school.example/news/abc/cover.png", storage.GetPublicUrl("news/abc/cover.png"));
    }

    [Fact]
    public async Task The_upload_url_carries_its_expiry()
    {
        var before = DateTimeOffset.UtcNow;

        var upload = await seaweed.Storage.CreateUploadUrlAsync(FileBucket.Public, NewKey(), "image/png", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.InRange(upload.ExpiresAt, before.AddMinutes(4.9), before.AddMinutes(5.1));
        Assert.Contains("X-Amz-Expires=300", upload.Url, StringComparison.Ordinal);
    }
}
