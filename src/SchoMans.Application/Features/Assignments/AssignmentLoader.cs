using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments;

internal static class AssignmentLoader
{
    public static async Task<AssignmentDto?> LoadAsync(IAppDbContext db, Guid id, bool canEdit, Guid? studentId, CancellationToken ct)
    {
        var row = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.DueAt,
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
            RowVersionCodec.Encode(row.RowVersion));
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
