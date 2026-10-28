using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Domain.Grading;
using SchoMans.Infrastructure.Persistence.Seeder;

namespace SchoMans.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Applies migrations and seeds demo data when the configuration asks for it (local development and the demo).</summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();

        if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(ct);
            await SeedDefaultGradeBandsAsync(db, ct);
        }

        if (configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
        }
    }

    /// <summary>The school starts with the usual A to E bands; an admin can change them afterwards. Reference data, not demo data.</summary>
    private static async Task SeedDefaultGradeBandsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.GradeBands.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        db.GradeBands.AddRange(
            GradeBand.Create("A", 90),
            GradeBand.Create("B", 80),
            GradeBand.Create("C", 70),
            GradeBand.Create("D", 60),
            GradeBand.Create("E", 0));
        await db.SaveChangesAsync(ct);
    }
}
