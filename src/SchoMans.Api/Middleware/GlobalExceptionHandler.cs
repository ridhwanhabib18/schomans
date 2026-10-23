using Microsoft.AspNetCore.Diagnostics;

namespace SchoMans.Api.Middleware;

/// <summary>Turns an unexpected exception into a 500 ProblemDetails. The exception text is logged, never sent to the client.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = "An unexpected error occurred.", Status = StatusCodes.Status500InternalServerError },
        });
    }
}
