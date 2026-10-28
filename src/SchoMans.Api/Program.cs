using Scalar.AspNetCore;
using SchoMans.Api.Auth;
using SchoMans.Api.Endpoints;
using SchoMans.Api.Extensions;
using SchoMans.Api.Hubs;
using SchoMans.Api.Middleware;
using SchoMans.Application.Common;
using SchoMans.Infrastructure;
using SchoMans.Infrastructure.Jobs;
using SchoMans.Infrastructure.Persistence;
using SchoMans.Infrastructure.Storage;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddSchoMansAuth(builder.Configuration)
    .AddSchoMansApi(builder.Configuration);

var app = builder.Build();

// The database first: Hangfire keeps its tables inside it and creates them as soon as the dashboard is mapped below.
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors(ServiceCollectionExtensions.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<UserSyncMiddleware>();

app.MapDashboardEndpoints();
app.MapUserEndpoints();
app.MapStudentEndpoints();
app.MapTeacherEndpoints();
app.MapClassroomEndpoints();
app.MapSubjectEndpoints();
app.MapAcademicYearEndpoints();
app.MapScheduleEndpoints();
app.MapAttendanceEndpoints();
app.MapAssignmentEndpoints();
app.MapGradeBandEndpoints();
app.MapGradeWeightEndpoints();
app.MapNewsEndpoints();
app.MapAnnouncementEndpoints();
app.MapNotificationEndpoints();
app.MapFileEndpoints();
app.MapSitemapEndpoints();
app.MapHealthEndpoints();
app.MapHub<NotificationHub>(NotificationHub.Path);
app.MapHub<AttendanceHub>(AttendanceHub.Path);
app.MapJobsDashboard();

// The API docs are not public in production.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await StorageInitializer.InitializeAsync(app.Services);
JobsSetup.RegisterRecurringJobs(app.Services);
await app.RunAsync();

// Makes the entry point visible to WebApplicationFactory in the integration tests.
public partial class Program;
