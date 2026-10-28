using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.GradeWeights;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Grading.GetGradeBook;

/// <summary>
/// The grade book of a subject in a classroom for a term: per student the average of each component, the final grade,
/// its band and whether it reaches the minimum passing score. The teacher of the subject in that classroom and the
/// principal see it.
/// </summary>
public sealed class GetGradeBookHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<GradeBookDto>> Handle(GetGradeBookQuery query, CancellationToken ct)
    {
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == query.SubjectId)
            .Select(s => new { s.MinimumPassingScore }).FirstOrDefaultAsync(ct);
        if (subject is null || !await db.Classrooms.AnyAsync(c => c.Id == query.ClassroomId, ct) || !await db.Terms.AnyAsync(t => t.Id == query.TermId, ct))
        {
            return Error.NotFound("grades.not_found", "The classroom, subject or term does not exist.");
        }

        if (currentUser.Role == UserRole.Teacher)
        {
            var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
            if (teacherId is null || !await GradeBook.TeachesAsync(db, teacherId.Value, query.SubjectId, query.ClassroomId, ct))
            {
                return Error.Forbidden("grades.not_your_class", "You only see the grades of the subjects you teach in your classrooms.");
            }
        }
        else if (currentUser.Role != UserRole.Principal)
        {
            return Error.Forbidden("grades.forbidden", "You are not allowed to see the grade book.");
        }

        var students = await db.Students.AsNoTracking()
            .Where(s => s.ClassroomId == query.ClassroomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.User!.FullName)
            .Select(s => new { s.Id, s.User!.FullName, s.Nisn })
            .ToListAsync(ct);

        var studentIds = students.Select(s => s.Id).ToList();
        var scores = await db.Grades.AsNoTracking()
            .Where(g => g.SubjectId == query.SubjectId && g.TermId == query.TermId && studentIds.Contains(g.StudentId))
            .Select(g => new { g.StudentId, g.Component, g.Score })
            .ToListAsync(ct);
        var scoresByStudent = scores.ToLookup(g => g.StudentId);

        var weights = await GradeWeightsLoader.LoadAsync(db, query.SubjectId, ct);
        var weightMap = weights.Weights.ToDictionary(w => w.Component, w => w.WeightPercent);
        var bands = await db.GradeBands.AsNoTracking().ToListAsync(ct);

        var rows = students.Select(student =>
        {
            var own = scoresByStudent[student.Id].ToList();
            var components = Enum.GetValues<GradeComponent>().Select(component =>
            {
                var ofComponent = own.Where(g => g.Component == component).ToList();
                return new ComponentScoreDto(component, ofComponent.Count == 0 ? null : decimal.Round(ofComponent.Average(g => g.Score), 2), ofComponent.Count);
            }).ToList();

            var final = GradeCalculator.FinalGrade(weightMap, own.Select(g => (g.Component, g.Score)));
            return new GradeBookRowDto(
                student.Id,
                student.FullName,
                student.Nisn,
                components,
                final,
                final is null ? null : GradeBand.LabelFor(final.Value, bands),
                final is null ? null : final >= subject.MinimumPassingScore);
        }).ToList();

        var published = await GradeBook.IsPublishedAsync(db, query.ClassroomId, query.TermId, ct);
        return new GradeBookDto(query.ClassroomId, query.SubjectId, query.TermId, subject.MinimumPassingScore, published, weights, rows);
    }
}
