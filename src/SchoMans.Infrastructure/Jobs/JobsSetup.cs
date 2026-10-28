using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure.Jobs;

/// <summary>Configuration section <c>Jobs</c>.</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Runs the Hangfire server and its recurring jobs. Off in the integration tests, which call the handlers directly.</summary>
    public bool Enabled { get; set; } = true;
}

public static class JobsSetup
{
    public const string RotateQrJobId = "rotate-attendance-qr";
    public const string CloseStaleSessionsJobId = "close-stale-attendance-sessions";
    public const string DueReminderJobId = "assignment-due-reminder";

    /// <summary>Hangfire with SQL Server storage in its own schema of the application database.</summary>
    public static IServiceCollection AddSchoolJobs(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.Configure<JobsOptions>(configuration.GetSection(JobsOptions.SectionName));
        if (!configuration.GetValue(JobsOptions.SectionName + ":" + nameof(JobsOptions.Enabled), true))
        {
            return services;
        }

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                () => new SqlConnection(connectionString),
                new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                }));
        services.AddHangfireServer(options => options.WorkerCount = Math.Min(Environment.ProcessorCount, 4));

        services.AddScoped<RotateAttendanceQrJob>();
        services.AddScoped<CloseStaleSessionsJob>();
        services.AddScoped<AssignmentDueReminderJob>();
        return services;
    }

    /// <summary>Registers (or updates) the recurring jobs. Called once at startup, after the database is migrated.</summary>
    public static void RegisterRecurringJobs(IServiceProvider services)
    {
        if (!services.GetRequiredService<IOptions<JobsOptions>>().Value.Enabled)
        {
            return;
        }

        var jobs = services.GetRequiredService<IRecurringJobManager>();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(services.GetRequiredService<IOptions<SchoolOptions>>().Value.TimeZone);

        jobs.AddOrUpdate<RotateAttendanceQrJob>(RotateQrJobId, job => job.RunAsync(CancellationToken.None), Cron.Minutely());
        jobs.AddOrUpdate<CloseStaleSessionsJob>(CloseStaleSessionsJobId, job => job.RunAsync(CancellationToken.None), "*/15 * * * *");
        jobs.AddOrUpdate<AssignmentDueReminderJob>(
            DueReminderJobId,
            job => job.RunAsync(CancellationToken.None),
            Cron.Daily(7),
            new RecurringJobOptions { TimeZone = zone });
    }
}
