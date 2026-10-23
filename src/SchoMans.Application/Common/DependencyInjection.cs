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

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
