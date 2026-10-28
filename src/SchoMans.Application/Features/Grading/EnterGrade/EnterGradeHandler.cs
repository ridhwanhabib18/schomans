using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.EnterGrade;

/// <summary>
/// A teacher enters or corrects the score of a student for a quiz, the midterm or the final. One such score per
/// student, subject, term and component. Refused once the grades of the student's classroom are published for the term.
/// </summary>
public sealed class EnterGradeHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<EnteredGradeDto>> Handle(EnterGradeCommand command, CancellationToken ct)
    {
        var classroomId = await db.Students.Where(s => s.Id == command.StudentId).Select(s => (Guid?)s.ClassroomId).FirstOrDefaultAsync(ct);
        if (classroomId is null || !await db.Subjects.AnyAsync(s => s.Id == command.SubjectId, ct) || !await db.Terms.AnyAsync(t => t.Id == command.TermId, ct))
        {
            return Error.NotFound("grades.not_found", "The student, subject or term does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null || !await GradeBook.TeachesAsync(db, teacherId.Value, command.SubjectId, classroomId.Value, ct))
        {
            return Error.Forbidden("grades.not_your_class", "You only grade the subjects you teach in your classrooms.");
        }

        if (await GradeBook.IsPublishedAsync(db, classroomId.Value, command.TermId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a score.");
        }

        var grade = await db.Grades.FirstOrDefaultAsync(
            g => g.StudentId == command.StudentId && g.SubjectId == command.SubjectId && g.TermId == command.TermId
                && g.Component == command.Component && g.SubmissionId == null,
            ct);
        if (grade is null)
        {
            grade = Grade.Create(command.StudentId, command.SubjectId, command.TermId, command.Component, command.Score);
            db.Grades.Add(grade);
        }
        else
        {
            grade.SetScore(command.Score);
        }

        await db.SaveChangesAsync(ct);
        return new EnteredGradeDto(grade.Id, grade.StudentId, grade.SubjectId, grade.TermId, grade.Component, grade.Score);
    }
}
