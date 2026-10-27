using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Files.CreateUploadUrl;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>Uploads and the endpoints open to anybody are rate limited (PRD security). The limits come from configuration, so the tests use tiny ones.</summary>
[Collection(ApiCollection.Name)]
public class RateLimitTests(ApiFixture fixture)
{
    [Fact]
    public async Task Upload_urls_are_limited_per_user()
    {
        await using var limited = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:UploadsPerMinute"] = "2" });
        using var admin = limited.AsUser("admin@schomans.demo", "Sari Utami", "sub-demo-admin", "Admin");
        using var principal = limited.AsUser("principal@schomans.demo", "Hendra Wirawan", "sub-demo-principal", "Principal");
        var command = new CreateUploadUrlCommand("news", Guid.NewGuid(), "image/png", 1024); // the record does not exist: still counts as a request

        var first = await admin.PostJsonAsync("/api/v1/files/upload-url", command);
        var second = await admin.PostJsonAsync("/api/v1/files/upload-url", command);
        var third = await admin.PostJsonAsync("/api/v1/files/upload-url", command);
        var otherUser = await principal.PostJsonAsync("/api/v1/files/upload-url", command);

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherUser.StatusCode); // somebody else has their own allowance
    }

    [Fact]
    public async Task Imports_share_the_upload_allowance()
    {
        await using var limited = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:UploadsPerMinute"] = "1" });
        using var admin = limited.AsUser("admin@schomans.demo", "Sari Utami", "sub-demo-admin", "Admin");

        using var first = TestData.FileContent(TestData.Workbook(), "students.xlsx");
        using var second = TestData.FileContent(TestData.Workbook(), "students.xlsx");
        var one = await admin.PostAsync("/api/v1/students/import", first);
        var two = await admin.PostAsync("/api/v1/students/import", second);

        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, two.StatusCode);
    }

    [Fact]
    public async Task The_public_pages_are_limited_per_client()
    {
        await using var limited = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:PublicPerMinute"] = "3" });
        using var guest = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await guest.GetAsync("/api/v1/news/public")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task The_public_announcements_and_the_sitemap_share_the_public_allowance_but_ordinary_endpoints_are_not_limited()
    {
        await using var limited = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:PublicPerMinute"] = "2", ["RateLimiting:UploadsPerMinute"] = "1" });
        using var guest = limited.CreateClient();
        using var teacher = limited.AsUser("teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/announcements/public")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/sitemap.xml")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await guest.GetAsync("/api/v1/announcements/public")).StatusCode);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync("/api/v1/dashboard")).StatusCode);
        }
    }
}
