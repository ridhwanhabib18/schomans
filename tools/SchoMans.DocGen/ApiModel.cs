using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Domain.Students;

namespace SchoMans.DocGen;

/// <summary>One operation of the API as the router knows it.</summary>
public sealed record Operation(
    string Method,
    string Route,
    string Name,
    string Group,
    IReadOnlyList<string> Policies,
    bool Anonymous,
    string? Request,
    string? Response,
    string? RateLimit);

/// <summary>The roles of the application, in the order the matrix shows them.</summary>
public static class AppRoles
{
    public static readonly IReadOnlyList<string> All = Enum.GetNames<UserRole>();
}

public sealed record PolicyInfo(string Name, IReadOnlyList<string> Roles);

public static class ApiModel
{
    /// <summary>Reads every routed endpoint, and the roles each policy allows, from the host that was built like the real API.</summary>
    public static async Task<(IReadOnlyList<Operation> Operations, IReadOnlyList<PolicyInfo> Policies)> ReadAsync(IEndpointRouteBuilder routes, IServiceProvider services)
    {
        // Endpoints mapped on the application are only added to the router when it runs, so they are read from the data sources.
        var source = new CompositeEndpointDataSource(routes.DataSources);
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policies = new Dictionary<string, PolicyInfo>(StringComparer.Ordinal);
        var operations = new List<Operation>();

        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var route = "/" + endpoint.RoutePattern.RawText!.Trim('/');
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods is null || methods.Count == 0)
            {
                // A hub is routed without an HTTP method of its own: SignalR negotiates over POST and then upgrades the connection.
                if (endpoint.Metadata.Any(m => m.GetType().Name == "HubMetadata"))
                {
                    methods = ["WS"];
                }
                else
                {
                    continue;
                }
            }

            var authorize = endpoint.Metadata.OfType<IAuthorizeData>().ToList();
            var names = authorize.Where(a => !string.IsNullOrEmpty(a.Policy)).Select(a => a.Policy!).Distinct().ToList();
            foreach (var name in names.Where(name => !policies.ContainsKey(name)))
            {
                var policy = await provider.GetPolicyAsync(name);
                var roles = policy?.Requirements.OfType<RolesAuthorizationRequirement>().SelectMany(r => r.AllowedRoles).Distinct().ToList() ?? [];
                policies[name] = new PolicyInfo(name, roles);
            }

            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null || authorize.Count == 0;
            var tags = endpoint.Metadata.GetMetadata<ITagsMetadata>()?.Tags;
            var group = tags is { Count: > 0 } ? tags[0] : GroupOf(route);
            var accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;
            var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(p => p.StatusCode is >= 200 and < 300)
                .Select(p => (p.StatusCode, p.Type))
                .FirstOrDefault();
            var rate = endpoint.Metadata.OfType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>().FirstOrDefault()?.PolicyName;

            foreach (var method in methods.Where(m => m != "HEAD" && m != "OPTIONS"))
            {
                operations.Add(new Operation(
                    method,
                    route,
                    endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? string.Empty,
                    group,
                    names,
                    anonymous,
                    accepts is null || accepts == typeof(void) ? null : TypeName(accepts),
                    produces.Type is null || produces.Type == typeof(void) ? null : TypeName(produces.Type),
                    rate));
            }
        }

        return (
            operations.OrderBy(o => o.Group, StringComparer.Ordinal).ThenBy(o => o.Route, StringComparer.Ordinal).ThenBy(o => o.Method, StringComparer.Ordinal).ToList(),
            policies.Values.OrderBy(p => p.Name, StringComparer.Ordinal).ToList());
    }

    /// <summary>The roles that may call an operation: those every one of its policies allows. Null means anybody may call it.</summary>
    public static IReadOnlyList<string>? AllowedRoles(Operation operation, IReadOnlyList<PolicyInfo> policies)
    {
        if (operation.Anonymous)
        {
            return null;
        }

        IEnumerable<string> allowed = AppRoles.All;
        foreach (var name in operation.Policies)
        {
            var roles = policies.First(p => p.Name == name).Roles;
            if (roles.Count > 0)
            {
                allowed = allowed.Intersect(roles);
            }
        }

        return allowed.ToList();
    }

    /// <summary>A route as a document names it: no constraints, no case, and every parameter just <c>{}</c>.</summary>
    public static string Normalize(string route)
    {
        var withoutQuery = route.Split('?')[0].TrimEnd('/').ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.Replace(withoutQuery, @"\{[^}]*\}", "{}");
    }

    private static string GroupOf(string route)
    {
        var parts = route.Trim('/').Split('/');
        return parts.Length >= 3 && parts[0] == "api" ? parts[2] : parts[0];
    }

    private static string TypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        return type.Name;
    }
}
