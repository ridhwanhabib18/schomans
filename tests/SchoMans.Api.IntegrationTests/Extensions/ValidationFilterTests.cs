using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Users.SyncUser;

namespace SchoMans.Api.IntegrationTests.Extensions;

public class ValidationFilterTests
{
    private static readonly EndpointFilterDelegate Next = _ => ValueTask.FromResult<object?>("handler ran");

    private static async Task<object?> Run(params object?[] arguments)
    {
        var services = new ServiceCollection()
            .AddSingleton<IValidator<SyncUserCommand>, SyncUserValidator>()
            .BuildServiceProvider();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext { RequestServices = services }, arguments);

        return await new ValidationFilter<SyncUserCommand>().InvokeAsync(context, Next);
    }

    [Fact]
    public async Task Valid_model_reaches_the_handler()
    {
        var result = await Run(new SyncUserCommand("sub", "a@b.test", "A B", ["Teacher"]));

        Assert.Equal("handler ran", result);
    }

    [Fact]
    public async Task Invalid_model_returns_a_validation_problem_and_skips_the_handler()
    {
        var result = await Run(new SyncUserCommand("sub", "not-an-email", "A B", ["Teacher"]));

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains(nameof(SyncUserCommand.Email), problem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task Missing_body_returns_a_validation_problem()
    {
        var result = await Run("some other argument");

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }
}
