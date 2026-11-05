using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments;

internal static class AssignmentLoader
{
    /// <param name="now">When given, the score of a quiz is left out of a student's own work until the quiz is due: a student sees the result only then.</param>
    public static async Task<AssignmentDto?> LoadAsync(IAppDbContext db, Guid id, bool canEdit, Guid? studentId, CancellationToken ct, DateTimeOffset? now = null)
    {
        var row = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.DueAt,
                a.Type,
                a.SubjectId,
                SubjectName = a.Subject!.Name,
                a.TeacherId,
                TeacherName = a.Teacher!.User!.FullName,
                TermName = a.Term!.Name,
                ClassroomIds = a.Classrooms.Select(c => c.ClassroomId).ToList(),
                Attachments = a.Attachments.OrderBy(f => f.FileName).Select(f => new AttachmentDto(f.Id, f.FileName)).ToList(),
                a.CreatedAt,
                a.RowVersion,
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(c => row.ClassroomIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new AssignmentClassroomDto(c.Id, c.Name))
            .ToListAsync(ct);

        var submission = studentId is null ? null : await SubmissionOfAsync(db, id, studentId.Value, ct);
        if (submission is not null && row.Type == AssignmentType.Quiz && now is { } at && at < row.DueAt)
        {
            submission = QuizRedaction.Hide(submission);
        }

        return new AssignmentDto(
            row.Id,
            row.Title,
            row.Description,
            row.DueAt,
            row.SubjectId,
            row.SubjectName,
            row.TeacherId,
            row.TeacherName,
            row.TermName,
            classrooms,
            row.Attachments,
            submission,
            canEdit,
            row.CreatedAt,
            RowVersionCodec.Encode(row.RowVersion),
            row.Type);
    }

    public static Task<SubmissionDto?> SubmissionOfAsync(IAppDbContext db, Guid assignmentId, Guid studentId, CancellationToken ct) =>
        db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId && s.StudentId == studentId)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);

    public static readonly System.Linq.Expressions.Expression<Func<Submission, SubmissionDto>> Projection = s => new SubmissionDto(
        s.Id,
        s.AssignmentId,
        s.StudentId,
        s.Student!.User!.FullName,
        s.FileName,
        s.FileKey != null,
        s.TextBody,
        s.SubmittedAt,
        s.IsLate,
        s.Score,
        s.Feedback,
        s.GradedAt);
}
