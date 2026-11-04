using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SchoMans.Application.Common;

public static class DependencyInjection
{
    /// <summary>Registers every use case handler (plain classes, no mediator) and every validator.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            {
                services.AddScoped(type);
            }
        }

        services.AddScoped<Features.Ai.AiGateway>();
        services.AddScoped<Features.ProgressSummaries.ProgressSummaryDrafter>();
        services.AddScoped<Features.AchievementDescriptions.AchievementDescriptionDrafter>();
        services.AddScoped<Features.EssayFeedback.EssayFeedbackDrafter>();
        services.AddScoped<Features.Admissions.ExtractDocuments.DocumentExtractor>();
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
