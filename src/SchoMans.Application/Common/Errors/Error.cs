namespace SchoMans.Application.Common.Errors;

public enum ErrorType
{
    Failure,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

/// <summary>A business error. The <see cref="Code"/> is stable (for example <c>users.not_found</c>) and ends up in ProblemDetails.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
