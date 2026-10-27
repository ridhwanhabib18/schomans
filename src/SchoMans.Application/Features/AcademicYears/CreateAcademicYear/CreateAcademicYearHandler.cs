using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.AcademicYears.CreateAcademicYear;

public sealed class CreateAcademicYearHandler(IAppDbContext db)
{
    public async Task<Result<AcademicYearDto>> Handle(CreateAcademicYearCommand command, CancellationToken ct)
    {
        if (AcademicYearMapping.CheckYearShape(command.StartDate, command.EndDate, command.Terms) is { } problem)
        {
            return Error.Validation("academic_years.invalid", problem);
        }

        var name = command.Name.Trim();
        if (await db.AcademicYears.AnyAsync(y => y.Name == name, ct))
        {
            return Error.Conflict("academic_years.name_taken", "An academic year with this name already exists.");
        }

        var year = AcademicYear.Create(name, command.StartDate, command.EndDate, isActive: false);
        db.AcademicYears.Add(year);
        foreach (var (term, index) in command.Terms.OrderBy(t => t.StartDate).Select((t, i) => (t, i)))
        {
            db.Terms.Add(Term.Create(year.Id, term.Name, term.StartDate, term.EndDate, isActive: index == 0));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("academic_years.name_taken", "An academic year with this name already exists.");
        }

        return (await AcademicYearMapping.LoadAsync(db, [year.Id], ct))[0];
    }
}
