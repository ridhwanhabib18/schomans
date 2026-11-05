using Microsoft.AspNetCore.HttpOverrides;

namespace SchoMans.Api.Extensions;

public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Behind a reverse proxy (Caddy in the deployment) every connection comes from the proxy, so the rate limits per client address
    /// would put all visitors in one bucket. With <c>ForwardedHeaders:Enabled</c> the client address and the scheme are read from
    /// <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>, but only when the connection comes from one of
    /// <c>ForwardedHeaders:TrustedNetworks</c> (CIDR ranges, for example the Docker network): anybody else could forge the headers.
    /// </summary>
    public static IServiceCollection AddSchoMansForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("ForwardedHeaders:Enabled", false))
        {
            return services;
        }

        var networks = configuration.GetSection("ForwardedHeaders:TrustedNetworks").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    public static WebApplication UseSchoMansForwardedHeaders(this WebApplication app)
    {
        if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
