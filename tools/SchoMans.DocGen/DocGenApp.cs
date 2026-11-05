using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Infrastructure;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.DocGen;

/// <summary>
/// Writes the documents that can be derived from the code (the ERD, the API reference, the authorization matrix, the jobs and the
/// real-time events) and checks that the hand-written diagrams only name API routes that still exist.
/// <code>dotnet run --project tools/SchoMans.DocGen            # write the files
/// dotnet run --project tools/SchoMans.DocGen -- --check   # fail when a file is out of date or a diagram names a route that is gone
/// dotnet run --project tools/SchoMans.DocGen -- --drift-only   # only the second of those (what a pull request checks)</code>
/// </summary>
public static class DocGenApp
{
    public static async Task<int> Main(string[] args)
    {
        var driftOnly = args.Contains("--drift-only");
        var check = args.Contains("--check") || driftOnly;
        var root = FindRepositoryRoot();
        var docs = Path.Combine(root, "docs", "en");

        // The API is built the way Program.cs builds it, with settings that point nowhere: nothing connects to anything.
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=docgen.invalid;Database=docgen;User Id=docgen;Password=docgen;Encrypt=False",
            ["Jobs:Enabled"] = "false",
            ["Keycloak:Authority"] = "http://keycloak.invalid/realms/schomans",
            ["Storage:Endpoint"] = "http://storage.invalid",
            ["Storage:AccessKey"] = "docgen",
            ["Storage:SecretKey"] = "docgen",
        });
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration).AddSchoMansAuth(builder.Configuration).AddSchoMansApi(builder.Configuration);
        await using var app = builder.Build();
        app.MapSchoMansEndpoints();

        var (operations, policies) = await ApiModel.ReadAsync(app, app.Services);
        string erd;
        IReadOnlyList<(string Domain, string Diagram)> byDomain;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            erd = Generators.Erd(db);
            byDomain = Generators.ErdByDomain(db);
        }

        // The design document keeps its own words; only the parts between docgen markers are written by the tool.
        var designPath = Path.Combine(docs, "system-design.md");
        var design = File.ReadAllText(designPath).Replace("\r\n", "\n");
        design = Region(design, "data-model", string.Join("\n\n", byDomain.Select(d => $"### {d.Domain}\n\n```mermaid\n{d.Diagram.TrimEnd()}\n```")));
        design = Region(design, "api-groups", Generators.ApiGroups(operations, policies));
        design = Region(design, "jobs-and-realtime", Generators.JobsAndRealtimeBody("###"));

        var files = new Dictionary<string, string>
        {
            [Path.Combine(docs, "diagrams", "erd.mmd")] = erd,
            [Path.Combine(docs, "generated", "api-reference.md")] = Generators.ApiReference(operations),
            [Path.Combine(docs, "generated", "authorization-matrix.md")] = Generators.AuthorizationMatrix(operations, policies),
            [Path.Combine(docs, "generated", "jobs-and-realtime.md")] = Generators.JobsAndRealtime(),
            [designPath] = design,
        };

        var failures = new List<string>();
        foreach (var (path, content) in files)
        {
            var text = content.Replace("\r\n", "\n");
            if (driftOnly)
            {
                continue;
            }

            if (check)
            {
                var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
                if (current != text)
                {
                    failures.Add($"{Path.GetRelativePath(root, path)} is out of date; run the tool without --check and commit the result.");
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
                Console.WriteLine($"wrote {Path.GetRelativePath(root, path)}");
            }
        }

        failures.AddRange(Drift(root, docs, operations, files));
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        if (failures.Count > 0)
        {
            return 1;
        }

        Console.WriteLine(check ? "The generated documents are up to date and the diagrams match the API." : $"{operations.Count} operations documented.");
        return 0;
    }

    /// <summary>
    /// A hand-written diagram or the system design may mention API routes and hubs. Each one must still exist: the same route, or the
    /// start of a route (a document may name just the base of a feature, such as <c>/api/v1/attendance</c>).
    /// </summary>
    private static IEnumerable<string> Drift(string root, string docs, IReadOnlyList<Operation> operations, IReadOnlyDictionary<string, string> generated)
    {
        var routes = operations.Select(o => ApiModel.Normalize(o.Route)).Distinct().ToList();
        var documents = Directory.GetFiles(Path.Combine(docs, "diagrams"), "*.mmd", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith("erd.mmd", StringComparison.Ordinal))
            .Append(Path.Combine(docs, "system-design.md"))
            .Where(File.Exists);

        foreach (var document in documents)
        {
            // The design document is checked as it will be after this run, with the generated parts in it.
            var text = generated.TryGetValue(document, out var written) ? written : File.ReadAllText(document);
            foreach (Match match in Regex.Matches(text, @"(?<![A-Za-z0-9])/(?:api/v\d+|hubs)/[A-Za-z0-9_\-/{}:.]*"))
            {
                var referenced = ApiModel.Normalize(match.Value.TrimEnd('.', ',', ':'));
                var exists = routes.Any(route => route == referenced || route.StartsWith(referenced + "/", StringComparison.Ordinal));
                if (!exists)
                {
                    yield return $"{Path.GetRelativePath(root, document)} names {match.Value}, but the API has no such route.";
                }
            }
        }
    }

    /// <summary>Replaces what stands between <c>&lt;!-- docgen:name:start --&gt;</c> and <c>&lt;!-- docgen:name:end --&gt;</c>.</summary>
    private static string Region(string text, string name, string content)
    {
        var start = $"<!-- docgen:{name}:start -->";
        var end = $"<!-- docgen:{name}:end -->";
        var from = text.IndexOf(start, StringComparison.Ordinal);
        var to = text.IndexOf(end, StringComparison.Ordinal);
        if (from < 0 || to < from)
        {
            throw new InvalidOperationException($"system-design.md has no region {start} ... {end}.");
        }

        return text[..(from + start.Length)] + "\n\n" + content.TrimEnd() + "\n\n" + text[to..];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SchoMans.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("SchoMans.sln was not found above " + AppContext.BaseDirectory);
    }
}
