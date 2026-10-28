namespace SchoMans.Application.Common.Errors;

/// <summary>The result of a use case that has nothing to return, for example a delete (the API answers 204).</summary>
public readonly record struct Unit;

public static class CommonErrors
{
    public static Error ConcurrencyConflict { get; } = Error.Conflict(
        "concurrency.conflict",
        "Somebody else changed this record since you opened it. Reload it and try again.");

    public static Error InvalidRowVersion { get; } = Error.Validation(
        "concurrency.invalid_row_version",
        "The row version is missing or not valid.");
}
