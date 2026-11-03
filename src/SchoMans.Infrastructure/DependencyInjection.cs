using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.Attendance;
using SchoMans.Infrastructure.Ai;
using SchoMans.Infrastructure.Content;
using SchoMans.Infrastructure.Excel;
using SchoMans.Infrastructure.Identity;
using SchoMans.Infrastructure.Jobs;
using SchoMans.Infrastructure.Pdf;
using SchoMans.Infrastructure.Persistence;
using SchoMans.Infrastructure.Persistence.Seeder;
using SchoMans.Infrastructure.Storage;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The connection string 'Default' is not configured.");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BaseEntitySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .AddInterceptors(provider.GetRequiredService<BaseEntitySaveChangesInterceptor>()));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<S3FileStorage>();
        services.AddSingleton<IFileStorage>(provider => provider.GetRequiredService<S3FileStorage>());

        services.AddSingleton<IHtmlContentSanitizer, HtmlContentSanitizer>();
        services.AddSingleton<IStudentImportParser, StudentImportParser>();
        services.AddSingleton<IStudentTemplateWriter, StudentTemplateWriter>();
        services.AddSingleton<IAttendanceExport, AttendanceExcelWriter>();
        services.AddSingleton<IReportCardPdf, ReportCardPdfRenderer>();
        services.AddSingleton<ILetterPdf, LetterPdfRenderer>();
        services.AddSingleton<IQrCodeGenerator, QrCoderGenerator>();
        services.AddSingleton<ISiteLinks, SiteLinks>();

        services.Configure<KeycloakAdminOptions>(configuration.GetSection(KeycloakAdminOptions.SectionName));
        services.AddHttpClient<IIdentityProvisioner, KeycloakIdentityProvisioner>(client => client.Timeout = TimeSpan.FromSeconds(10));

        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddAiService(configuration);

        services.Configure<SchoolOptions>(configuration.GetSection(SchoolOptions.SectionName));
        services.AddSingleton<ISchoolClock, SchoolClock>();
        services.Configure<AttendanceOptions>(configuration.GetSection(AttendanceOptions.SectionName));
        services.AddSchoolJobs(configuration, connectionString);

        services.AddScoped<DemoDataSeeder>();
        return services;
    }

    /// <summary>The provider is chosen once at start-up from <c>Ai:Provider</c> (ADR-004); the mock is the default.</summary>
    private static void AddAiService(this IServiceCollection services, IConfiguration configuration)
    {
        var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        switch (ai.Provider)
        {
            case AiProviders.Ollama:
                services.AddHttpClient<IAiService, OllamaAiService>(client => client.Timeout = TimeSpan.FromSeconds(ai.Ollama.TimeoutSeconds));
                break;
            case AiProviders.Claude:
                services.AddHttpClient<IAiService, ClaudeAiService>(client => client.Timeout = TimeSpan.FromSeconds(ai.Claude.TimeoutSeconds));
                break;
            case AiProviders.Mock:
                services.AddSingleton<IAiService, MockAiService>();
                break;
            default:
                throw new InvalidOperationException($"Ai:Provider '{ai.Provider}' is unknown; use {AiProviders.Mock}, {AiProviders.Ollama} or {AiProviders.Claude}.");
        }
    }
}
