using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance;
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

        services.Configure<KeycloakAdminOptions>(configuration.GetSection(KeycloakAdminOptions.SectionName));
        services.AddHttpClient<IIdentityProvisioner, KeycloakIdentityProvisioner>(client => client.Timeout = TimeSpan.FromSeconds(10));

        services.Configure<SchoolOptions>(configuration.GetSection(SchoolOptions.SectionName));
        services.AddSingleton<ISchoolClock, SchoolClock>();
        services.Configure<AttendanceOptions>(configuration.GetSection(AttendanceOptions.SectionName));
        services.AddSchoolJobs(configuration, connectionString);

        services.AddScoped<DemoDataSeeder>();
        return services;
    }
}
