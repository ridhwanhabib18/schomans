using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.AcademicYears.ActivateAcademicYear;

/// <summary>Makes an academic year the current one. Only one year is active at a time, enforced by a unique index.</summary>
public sealed class ActivateAcademicYearHandler(IAppDbContext db)
{
    public async Task<Result<AcademicYearDto>> Handle(ActivateAcademicYearCommand command, CancellationToken ct)
    {
        if (!await db.AcademicYears.AnyAsync(y => y.Id == command.Id, ct))
        {
            return Error.NotFound("academic_years.not_found", "The academic year does not exist.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Two saves: the old year must be inactive before the new one becomes active, or the unique index rejects it.
            foreach (var other in await db.AcademicYears.Where(y => y.IsActive && y.Id != command.Id).ToListAsync(ct))
            {
                other.Deactivate();
            }

            await db.SaveChangesAsync(ct);

            var year = await db.AcademicYears.FirstAsync(y => y.Id == command.Id, ct);
            year.Activate();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return (await AcademicYearMapping.LoadAsync(db, [command.Id], ct))[0];
    }
}
