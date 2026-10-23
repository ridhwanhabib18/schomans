namespace SchoMans.Application.Common;

/// <summary>Uniform list response: every paged endpoint takes <c>page</c> and <c>pageSize</c> (max 100).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public const int MaxPageSize = 100;
}
