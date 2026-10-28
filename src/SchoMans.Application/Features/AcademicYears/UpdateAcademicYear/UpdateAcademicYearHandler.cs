using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.AcademicYears.UpdateAcademicYear;

public sealed class UpdateAcademicYearHandler(IAppDbContext db)
{
    public async Task<Result<AcademicYearDto>> Handle(UpdateAcademicYearCommand command, CancellationToken ct)
    {
        var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.Id == command.Id, ct);
        if (year is null)
        {
            return Error.NotFound("academic_years.not_found", "The academic year does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var existingTerms = await db.Terms.Where(t => t.AcademicYearId == year.Id).ToListAsync(ct);

        // The terms after this update: the submitted ones plus the existing ones that were left out (never removed here).
        var submittedIds = command.Terms.Where(t => t.Id is not null).Select(t => t.Id!.Value).ToHashSet();
        var unknown = submittedIds.Except(existingTerms.Select(t => t.Id)).ToList();
        if (unknown.Count > 0)
        {
            return Error.NotFound("terms.not_found", "One of the terms does not belong to this academic year.");
        }

        var kept = existingTerms.Where(t => !submittedIds.Contains(t.Id)).Select(t => new TermInput(t.Id, t.Name, t.StartDate, t.EndDate));
        if (AcademicYearMapping.CheckYearShape(command.StartDate, command.EndDate, command.Terms.Concat(kept).ToList()) is { } problem)
        {
            return Error.Validation("academic_years.invalid", problem);
        }

        var name = command.Name.Trim();
        if (await db.AcademicYears.AnyAsync(y => y.Name == name && y.Id != year.Id, ct))
        {
            return Error.Conflict("academic_years.name_taken", "An academic year with this name already exists.");
        }

        year.Update(name, command.StartDate, command.EndDate);
        db.SetExpectedRowVersion(year, rowVersion);

        foreach (var input in command.Terms)
        {
            if (input.Id is { } termId)
            {
                existingTerms.First(t => t.Id == termId).Update(input.Name, input.StartDate, input.EndDate);
            }
            else
            {
                db.Terms.Add(Term.Create(year.Id, input.Name, input.StartDate, input.EndDate, isActive: false));
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("academic_years.name_taken", "An academic year or term with this name already exists.");
        }

        return (await AcademicYearMapping.LoadAsync(db, [year.Id], ct))[0];
    }
}
