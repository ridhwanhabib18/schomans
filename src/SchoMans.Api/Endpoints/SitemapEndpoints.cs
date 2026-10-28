using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.News.GetSitemapEntries;

namespace SchoMans.Api.Endpoints;

public static class SitemapEndpoints
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// <c>/sitemap.xml</c> lists the public pages for search engines. It lives at the root of the site, not under
    /// <c>/api/v1</c>, because crawlers look for it there; the reverse proxy routes it to the API.
    /// </summary>
    public static IEndpointRouteBuilder MapSitemapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GetSitemap)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .ExcludeFromDescription()
            .WithName("GetSitemap");

        return app;
    }

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> GetSitemap(
        GetSitemapEntriesHandler handler, IConfiguration configuration, CancellationToken ct)
    {
        var result = await handler.Handle(new GetSitemapEntriesQuery(), ct);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var baseUrl = (configuration["Site:PublicBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var urls = new List<XElement> { Url($"{baseUrl}/", null), Url($"{baseUrl}/news", null) };
        urls.AddRange(result.Value.Select(entry => Url($"{baseUrl}/news/{entry.Slug}", entry.LastModified)));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(SitemapNamespace + "urlset", urls));
        return TypedResults.Content(document.Declaration + Environment.NewLine + document.Root, "application/xml");
    }

    private static XElement Url(string location, DateTimeOffset? lastModified)
    {
        var url = new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", location));
        if (lastModified is { } at)
        {
            url.Add(new XElement(SitemapNamespace + "lastmod", at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return url;
    }
}
