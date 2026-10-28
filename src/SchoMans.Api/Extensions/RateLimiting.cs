using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SchoMans.Api.Extensions;

public static class RateLimitPolicies
{
    /// <summary>Uploads and imports: per signed-in user.</summary>
    public const string Uploads = "uploads";

    /// <summary>Endpoints open to anybody (public news and announcements): per client address.</summary>
    public const string Public = "public";
}

public static class RateLimiting
{
    /// <summary>
    /// Fixed-window limits, so one client cannot flood the upload and the public endpoints. The numbers come from
    /// configuration (<c>RateLimiting:UploadsPerMinute</c>, <c>RateLimiting:PublicPerMinute</c>).
    /// </summary>
    public static IServiceCollection AddSchoMansRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var uploadsPerMinute = configuration.GetValue("RateLimiting:UploadsPerMinute", 30);
        var publicPerMinute = configuration.GetValue("RateLimiting:PublicPerMinute", 120);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Uploads, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(uploadsPerMinute)));

            options.AddPolicy(RateLimitPolicies.Public, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(publicPerMinute)));
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}
