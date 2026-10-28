using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading;

/// <summary>Shared rules of the grade book: when grades are locked and how a scored submission becomes a grade.</summary>
internal static class GradeBook
{
    /// <summary>Grades of a classroom are locked once the homeroom teacher has published them for the term.</summary>
    public static Task<bool> IsPublishedAsync(IAppDbContext db, Guid classroomId, Guid termId, CancellationToken ct) =>
        db.GradePublications.AnyAsync(p => p.ClassroomId == classroomId && p.TermId == termId, ct);

    public static async Task<bool> IsStudentLockedAsync(IAppDbContext db, Guid studentId, Guid termId, CancellationToken ct)
    {
        var classroomId = await db.Students.Where(s => s.Id == studentId).Select(s => s.ClassroomId).FirstAsync(ct);
        return await IsPublishedAsync(db, classroomId, termId, ct);
    }

    /// <summary>Creates or updates the grade that belongs to a scored submission. The caller saves.</summary>
    public static async Task SyncSubmissionGradeAsync(IAppDbContext db, Submission submission, Assignment assignment, CancellationToken ct)
    {
        var score = submission.Score!.Value;
        var grade = await db.Grades.FirstOrDefaultAsync(g => g.SubmissionId == submission.Id, ct);
        if (grade is null)
        {
            db.Grades.Add(Grade.Create(submission.StudentId, assignment.SubjectId, assignment.TermId, assignment.Component, score, submission.Id));
            return;
        }

        grade.SetScore(score);
    }
}
