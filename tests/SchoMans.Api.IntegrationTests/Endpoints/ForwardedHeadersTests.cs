using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Chatbot;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// Behind the reverse proxy the rate limit per client address must use the address the proxy forwards, and only when the proxy is
/// a trusted one. The test server has no real connection, so a startup filter plays the proxy's address.
/// </summary>
[Collection(ApiCollection.Name)]
public class ForwardedHeadersTests(ApiFixture fixture)
{
    private const string Public = "/api/v1/chatbot/public";

    private sealed class ConnectionFrom(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, pipeline) =>
            {
                context.Connection.RemoteIpAddress = address;
                return pipeline();
            });
            next(app);
        };
    }

    private HttpClient ClientBehindProxy(string proxyAddress, bool enabled, out ApiFactory host)
    {
        var settings = new Dictionary<string, string>
        {
            ["RateLimiting:ChatbotPerMinute"] = "2",
            ["ForwardedHeaders:Enabled"] = enabled ? "true" : "false",
            ["ForwardedHeaders:TrustedNetworks:0"] = "10.0.0.0/8",
        };
        host = fixture.CreateFactory(settings);
        return host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(new ConnectionFrom(IPAddress.Parse(proxyAddress))))).CreateClient();
    }

    private static async Task<HttpStatusCode> AskAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Public) { Content = JsonContent.Create(new AskChatbotCommand("When is the open day?")) };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Every_visitor_behind_a_trusted_proxy_has_their_own_limit()
    {
        using var client = ClientBehindProxy("10.1.2.3", enabled: true, out var host);
        await using var _ = host;

        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await AskAsync(client, "203.0.113.1"));

        // Another visitor behind the same proxy is not held back by the first one.
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.2"));
    }

    [Fact]
    public async Task The_headers_of_a_connection_that_is_not_a_trusted_proxy_are_ignored()
    {
        // 198.51.100.7 is outside 10.0.0.0/8, so a forged X-Forwarded-For cannot give a visitor a fresh limit.
        using var client = ClientBehindProxy("198.51.100.7", enabled: true, out var host);
        await using var _ = host;

        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await AskAsync(client, "203.0.113.3"));
    }

    [Fact]
    public async Task Without_the_setting_the_headers_are_ignored()
    {
        using var client = ClientBehindProxy("10.1.2.3", enabled: false, out var host);
        await using var _ = host;

        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "203.0.113.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await AskAsync(client, "203.0.113.3"));
    }
}
