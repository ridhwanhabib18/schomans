using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.ReportCards.ListStudentTerms;

public sealed record ListStudentTermsQuery(Guid StudentId);

/// <param name="IsPublished">The homeroom teacher has published the grades of the student's classroom for this term.</param>
public sealed record StudentTermDto(Guid Id, string Name, string AcademicYearName, bool IsActive, bool IsPublished);

/// <summary>
/// The terms of the academic year of the student's classroom, for the term picker of the grades page. Students and
/// parents may not list the academic years (that is master data for staff), so this answers only for their own student.
/// </summary>
public sealed class ListStudentTermsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<List<StudentTermDto>>> Handle(ListStudentTermsQuery query, CancellationToken ct)
    {
        if (!ReportCardAccess.IsFamily(currentUser.Role) || !await ReportCardAccess.IsOwnAsync(db, currentUser, query.StudentId, ct))
        {
            return Error.Forbidden("grades.not_yours", "You only see the grades of yourself or your child.");
        }

        var classroom = await db.Students.AsNoTracking()
            .Where(s => s.Id == query.StudentId)
            .Select(s => new { s.ClassroomId, s.Classroom!.AcademicYearId, YearName = s.Classroom.AcademicYear!.Name })
            .FirstAsync(ct);
        var published = await db.GradePublications.AsNoTracking()
            .Where(p => p.ClassroomId == classroom.ClassroomId)
            .Select(p => p.TermId)
            .ToListAsync(ct);

        var terms = await db.Terms.AsNoTracking()
            .Where(t => t.AcademicYearId == classroom.AcademicYearId)
            .OrderBy(t => t.StartDate)
            .Select(t => new { t.Id, t.Name, t.IsActive })
            .ToListAsync(ct);

        return terms.Select(t => new StudentTermDto(t.Id, t.Name, classroom.YearName, t.IsActive, published.Contains(t.Id))).ToList();
    }
}
