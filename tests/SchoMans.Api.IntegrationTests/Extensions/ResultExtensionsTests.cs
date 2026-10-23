using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common.Errors;

namespace SchoMans.Api.IntegrationTests.Extensions;

public class ResultExtensionsTests
{
    [Fact]
    public void Success_maps_to_200_with_the_value()
    {
        Result<string> result = "hello";

        var http = result.ToHttpResult();

        var ok = Assert.IsType<Ok<string>>(http.Result);
        Assert.Equal("hello", ok.Value);
    }

    [Fact]
    public void Success_can_map_to_a_custom_response()
    {
        Result<string> result = "hello";

        var http = result.ToHttpResult(value => TypedResults.Created($"/things/{value}", value));

        Assert.IsType<Created<string>>(http);
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    public void Errors_map_to_problem_details_with_a_stable_code(ErrorType type, int expectedStatus)
    {
        Result<string> result = new Error("things.broken", "It broke.", type);

        var problem = Assert.IsType<ProblemHttpResult>(result.ToHttpResult().Result);

        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("things.broken", problem.ProblemDetails.Extensions["code"]);
        Assert.Equal("It broke.", problem.ProblemDetails.Title);
    }
}
