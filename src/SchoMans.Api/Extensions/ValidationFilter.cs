using FluentValidation;

namespace SchoMans.Api.Extensions;

/// <summary>Runs the FluentValidation validator of the request model before the handler. Replaces a mediator validation pipeline (ADR-001).</summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [string.Empty] = ["A request body is required."],
            });
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
