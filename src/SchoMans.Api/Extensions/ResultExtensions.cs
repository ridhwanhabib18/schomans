using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Application.Common.Errors;

namespace SchoMans.Api.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Maps a use case result to HTTP: success becomes 200, an error becomes ProblemDetails with a stable <c>code</c>.
    /// The typed union is what puts the response schema into the OpenAPI document (and so into the generated client).
    /// </summary>
    public static Results<Ok<T>, ProblemHttpResult> ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    /// <summary>Same mapping with a custom success response, for example 201 Created.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    public static ProblemHttpResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(
            statusCode: status,
            title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
