using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace SchoMans.Application.Common;

public static class PagingExtensions
{
    /// <summary>The uniform list rules: <c>page</c> starts at 1 and <c>pageSize</c> is between 1 and 100.</summary>
    public static void RuleForPaging<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, int>> page,
        Expression<Func<T, int>> pageSize)
    {
        validator.RuleFor(page).GreaterThanOrEqualTo(1);
        validator.RuleFor(pageSize).InclusiveBetween(1, PagedResult<T>.MaxPageSize);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
