namespace SchoMans.Api.Extensions;

public static class EndpointExtensions
{
    /// <summary>Validates the request model <typeparamref name="T"/> and records the 400 ValidationProblem response in OpenAPI.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
