using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Domain.Approvals;
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
            await SeedDefaultApprovalChainsAsync(db, ct);
        }

        if (configuration.GetValue<bool>("Database:SeedDemoData"))
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
        }
    }

    /// <summary>
    /// The chains the school starts with: a student's leave goes to the homeroom teacher and then the principal, a teacher's
    /// leave to the principal, a certificate letter to the admin staff and then the principal. Admin staff can change them.
    /// </summary>
    private static async Task SeedDefaultApprovalChainsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.ApprovalChainSteps.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        (RequestType Type, ApproverRole[] Roles)[] chains =
        [
            (RequestType.StudentLeave, [ApproverRole.HomeroomTeacher, ApproverRole.Principal]),
            (RequestType.TeacherLeave, [ApproverRole.Principal]),
            (RequestType.CertificateLetter, [ApproverRole.Admin, ApproverRole.Principal]),
        ];

        foreach (var (type, roles) in chains)
        {
            for (var i = 0; i < roles.Length; i++)
            {
                db.ApprovalChainSteps.Add(ApprovalChainStep.Create(type, i + 1, roles[i]));
            }
        }

        await db.SaveChangesAsync(ct);
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
