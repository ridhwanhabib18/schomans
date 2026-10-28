using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Assignments.GradeSubmission;

/// <summary>
/// The teacher of the assignment scores a piece of work and gives feedback; the student is notified. Scoring makes the
/// work final for the student. The score stays on the submission and also becomes a grade of the subject, unless the term's grades are published.
/// </summary>
public sealed class GradeSubmissionHandler(IAppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISchoolClock clock)
{
    public async Task<Result<SubmissionDto>> Handle(GradeSubmissionCommand command, CancellationToken ct)
    {
        var submission = await db.Submissions.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == command.SubmissionId, ct);
        if (submission is null)
        {
            return Error.NotFound("submissions.not_found", "The handed-in work does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != submission.Assignment!.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment can score the work.");
        }

        if (await GradeBook.IsStudentLockedAsync(db, submission.StudentId, submission.Assignment.TermId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a score.");
        }

        var firstTime = !submission.IsGraded;
        submission.Grade(command.Score, command.Feedback, currentUser.UserId, clock.UtcNow);
        await GradeBook.SyncSubmissionGradeAsync(db, submission, submission.Assignment, ct);
        await db.SaveChangesAsync(ct);

        var studentUserId = await db.Students.Where(s => s.Id == submission.StudentId).Select(s => s.UserId).FirstAsync(ct);
        await NotificationSender.SendAsync(
            db,
            notifications,
            [studentUserId],
            NotificationType.AssignmentGraded,
            firstTime ? $"Your work was scored: {submission.Assignment.Title}" : $"Your score was changed: {submission.Assignment.Title}",
            string.Create(CultureInfo.InvariantCulture, $"Score {command.Score:0.##}"),
            AssignmentNotices.LinkFor(submission.AssignmentId),
            ct);

        return await db.Submissions.AsNoTracking().Where(s => s.Id == submission.Id).Select(AssignmentLoader.Projection).FirstAsync(ct);
    }
}
