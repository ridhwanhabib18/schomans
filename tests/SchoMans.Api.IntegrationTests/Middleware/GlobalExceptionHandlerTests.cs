using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SchoMans.Api.Middleware;

namespace SchoMans.Api.IntegrationTests.Middleware;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_exception_becomes_a_500_problem_without_leaking_its_message()
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            services.GetRequiredService<IProblemDetailsService>());

        var handled = await handler.TryHandleAsync(http, new InvalidOperationException("secret connection string"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Contains("An unexpected error occurred.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret connection string", body, StringComparison.Ordinal);
    }
}
