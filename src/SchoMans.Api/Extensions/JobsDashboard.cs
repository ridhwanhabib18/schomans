using Hangfire;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Jobs;

namespace SchoMans.Api.Extensions;

/// <summary>
/// The Hangfire dashboard at <see cref="Path"/>, for admin staff only. A browser opens it as a page, so it cannot send the
/// bearer token in a header: the web app links to it with the token in the query string once, and this middleware moves
/// the token into an HttpOnly cookie scoped to the dashboard path and reloads without it. The cookie lives as long as
/// the token; after that the admin opens the dashboard from the app again.
/// </summary>
public static class JobsDashboard
{
    public const string Path = "/hangfire";
    public const string CookieName = "schomans_jobs";

    public static WebApplication MapJobsDashboard(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<JobsOptions>>().Value.Enabled)
        {
            return app;
        }

        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(Path) && context.Request.Query.ContainsKey("access_token"),
            branch => branch.Run(MoveTokenToCookie));

        app.MapHangfireDashboard(Path, new DashboardOptions
        {
            DashboardTitle = "SchoMans jobs",
            DisplayStorageConnectionString = false,
            AppPath = null,
            Authorization = [new AdminOnlyFilter()],
        });

        return app;
    }

    private static Task MoveTokenToCookie(HttpContext context)
    {
        if (context.User.IsInRole(nameof(UserRole.Admin)))
        {
            var expires = long.TryParse(context.User.FindFirst("exp")?.Value, out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp)
                : DateTimeOffset.UtcNow.AddMinutes(5);
            context.Response.Cookies.Append(CookieName, context.Request.Query["access_token"].ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = Path,
                Expires = expires,
            });
        }

        context.Response.Redirect(Path);
        return Task.CompletedTask;
    }

    private sealed class AdminOnlyFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole(nameof(UserRole.Admin));
    }
}
