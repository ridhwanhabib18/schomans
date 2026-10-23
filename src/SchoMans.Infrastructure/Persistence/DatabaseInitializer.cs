using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        }

        if (configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
        }
    }
}
