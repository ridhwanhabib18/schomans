using SchoMans.Application.Common.Errors;

namespace SchoMans.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_exposes_the_value_and_no_error()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_exposes_the_error_and_no_value()
    {
        Result<int> result = Error.NotFound("things.not_found", "No such thing.");

        Assert.True(result.IsFailure);
        Assert.Equal("things.not_found", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Failure)]
    public void Error_factories_set_the_matching_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "m"),
            ErrorType.Unauthorized => Error.Unauthorized("c", "m"),
            ErrorType.Forbidden => Error.Forbidden("c", "m"),
            ErrorType.NotFound => Error.NotFound("c", "m"),
            ErrorType.Conflict => Error.Conflict("c", "m"),
            _ => Error.Failure("c", "m"),
        };

        Assert.Equal(type, error.Type);
    }
}
