using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.GradeBands.ListGradeBands;

/// <summary>The bands of the school, highest first. Every signed-in role may read them: they explain the letters on the grades.</summary>
public sealed class ListGradeBandsHandler(IAppDbContext db)
{
    public async Task<Result<List<GradeBandDto>>> Handle(ListGradeBandsQuery query, CancellationToken ct) =>
        await db.GradeBands.AsNoTracking()
            .OrderByDescending(b => b.MinScore)
            .Select(b => new GradeBandDto(b.Id, b.Label, b.MinScore))
            .ToListAsync(ct);
}
