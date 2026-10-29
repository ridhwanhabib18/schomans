using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.ReportCards.GetStudentGrades;

public sealed record GetStudentGradesQuery(Guid StudentId, Guid TermId);

/// <summary>A student, or a parent through their child, sees the grades of a term once the homeroom teacher has published them.</summary>
public sealed class GetStudentGradesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<StudentGradesDto>> Handle(GetStudentGradesQuery query, CancellationToken ct)
    {
        if (!ReportCardAccess.IsFamily(currentUser.Role) || !await ReportCardAccess.IsOwnAsync(db, currentUser, query.StudentId, ct))
        {
            return Error.Forbidden("grades.not_yours", "You only see the grades of yourself or your child.");
        }

        var classroomId = await db.Students.Where(s => s.Id == query.StudentId).Select(s => s.ClassroomId).FirstAsync(ct);
        var publishedAt = await db.GradePublications.AsNoTracking()
            .Where(p => p.ClassroomId == classroomId && p.TermId == query.TermId)
            .Select(p => (DateTimeOffset?)p.PublishedAt)
            .FirstOrDefaultAsync(ct);
        if (publishedAt is null)
        {
            return Error.NotFound("grades.not_published", "The grades of this term are not published yet.");
        }

        var results = await Transcript.BuildAsync(db, query.StudentId, query.TermId, ct);
        var reportCard = await db.ReportCards.AsNoTracking()
            .Where(r => r.StudentId == query.StudentId && r.TermId == query.TermId)
            .Select(r => new
            {
                HasPdf = r.PdfKey != null && r.Status == ReportCardStatus.Published,
                Descriptions = r.Descriptions.Where(d => d.Status == AchievementStatus.Accepted).Select(d => new { d.SubjectId, d.Body }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        var descriptions = reportCard?.Descriptions.ToDictionary(d => d.SubjectId, d => d.Body) ?? [];

        var subjects = results.Select(r => new SubjectGradeDto(
            r.SubjectId, r.SubjectName, r.MinimumPassingScore, r.Components, r.FinalGrade, r.Band, r.IsPassing, descriptions.GetValueOrDefault(r.SubjectId))).ToList();
        return new StudentGradesDto(query.StudentId, query.TermId, publishedAt.Value, reportCard?.HasPdf ?? false, subjects);
    }
}
