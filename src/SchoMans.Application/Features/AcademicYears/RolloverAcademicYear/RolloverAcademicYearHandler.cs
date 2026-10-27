using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;

public sealed class RolloverAcademicYearHandler(IAppDbContext db)
{
    public async Task<Result<RolloverResult>> Handle(RolloverAcademicYearCommand command, CancellationToken ct)
    {
        if (AcademicYearMapping.CheckYearShape(command.StartDate, command.EndDate, command.Terms) is { } problem)
        {
            return Error.Validation("academic_years.invalid", problem);
        }

        var current = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsActive, ct);
        if (current is null)
        {
            return Error.Conflict("academic_years.no_active_year", "There is no current academic year to roll over from.");
        }

        var name = command.Name.Trim();
        if (await db.AcademicYears.AnyAsync(y => y.Name == name, ct))
        {
            return Error.Conflict("academic_years.name_taken", "An academic year with this name already exists.");
        }

        if (command.StartDate <= current.StartDate)
        {
            return Error.Validation("academic_years.rollover_dates", "The new academic year must start after the current one starts.");
        }

        // Every classroom of the current year needs exactly one decision; a forgotten classroom would strand its students.
        var currentClassroomIds = await db.Classrooms.AsNoTracking().Where(c => c.AcademicYearId == current.Id).Select(c => c.Id).ToListAsync(ct);
        var mappedIds = command.Mappings.Select(m => m.FromClassroomId).ToList();
        if (mappedIds.Distinct().Count() != mappedIds.Count || !currentClassroomIds.ToHashSet().SetEquals(mappedIds))
        {
            return Error.Validation(
                "academic_years.rollover_mapping_incomplete",
                "Every classroom of the current academic year must be mapped exactly once.");
        }

        // A new classroom can be the target of several old ones (for example two classes merged into one).
        var newClassrooms = command.Mappings
            .Where(m => !m.Graduate)
            .GroupBy(m => m.ToClassroomName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.Key, TeacherId: g.Select(m => m.HomeroomTeacherId).First(t => t is not null)!.Value))
            .ToList();

        var teacherIds = newClassrooms.Select(c => c.TeacherId).Distinct().ToList();
        if (await db.Teachers.CountAsync(t => teacherIds.Contains(t.Id), ct) != teacherIds.Count)
        {
            return Error.NotFound("teachers.not_found", "One of the homeroom teachers does not exist.");
        }

        RolloverResult? result = null;
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear(); // a retry must start from a clean slate
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                // The old year becomes inactive first: only one year can be active at a time (unique index).
                var old = await db.AcademicYears.FirstAsync(y => y.Id == current.Id, ct);
                old.Deactivate();
                await db.SaveChangesAsync(ct);

                var year = AcademicYear.Create(name, command.StartDate, command.EndDate, isActive: true);
                db.AcademicYears.Add(year);
                foreach (var (term, index) in command.Terms.OrderBy(t => t.StartDate).Select((t, i) => (t, i)))
                {
                    db.Terms.Add(Term.Create(year.Id, term.Name, term.StartDate, term.EndDate, isActive: index == 0));
                }

                var created = newClassrooms.ToDictionary(
                    c => c.Name,
                    c => Classroom.Create(c.Name, year.Id, c.TeacherId),
                    StringComparer.OrdinalIgnoreCase);
                db.Classrooms.AddRange(created.Values);

                var students = await db.Students
                    .Where(s => currentClassroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active)
                    .ToListAsync(ct);
                var byClassroom = command.Mappings.ToDictionary(m => m.FromClassroomId);

                int moved = 0, graduated = 0;
                foreach (var student in students)
                {
                    var mapping = byClassroom[student.ClassroomId];
                    if (mapping.Graduate)
                    {
                        student.Graduate();
                        graduated++;
                    }
                    else
                    {
                        student.MoveTo(created[mapping.ToClassroomName!.Trim()].Id);
                        moved++;
                    }
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                result = new RolloverResult(year.Id, moved, graduated, created.Count);
            });
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("academic_years.rollover_conflict", "The rollover conflicted with another change. Nothing was changed; try again.");
        }

        return result!;
    }
}
