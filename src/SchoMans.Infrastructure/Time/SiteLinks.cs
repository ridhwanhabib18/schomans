using Microsoft.Extensions.Configuration;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Time;

/// <summary>Builds addresses of the public web app from the setting <c>Site:PublicBaseUrl</c>.</summary>
public sealed class SiteLinks(IConfiguration configuration) : ISiteLinks
{
    public string PublicUrl(string path)
    {
        var baseUrl = (configuration["Site:PublicBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/{path.TrimStart('/')}";
    }
}
