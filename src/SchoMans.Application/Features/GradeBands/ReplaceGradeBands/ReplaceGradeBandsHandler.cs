using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeBands.ReplaceGradeBands;

/// <summary>
/// The admin sets the whole scale in one go. The old bands are removed first and the new ones added in a second save,
/// both in one transaction, because the labels and minimums are unique and a swap would clash row by row.
/// </summary>
public sealed class ReplaceGradeBandsHandler(IAppDbContext db)
{
    public async Task<Result<List<GradeBandDto>>> Handle(ReplaceGradeBandsCommand command, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // a retry must start from a clean slate
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.GradeBands.RemoveRange(await db.GradeBands.ToListAsync(ct));
            await db.SaveChangesAsync(ct);

            db.GradeBands.AddRange(command.Bands.Select(b => GradeBand.Create(b.Label, b.MinScore)));
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        });

        return await db.GradeBands.AsNoTracking()
            .OrderByDescending(b => b.MinScore)
            .Select(b => new GradeBandDto(b.Id, b.Label, b.MinScore))
            .ToListAsync(ct);
    }
}
