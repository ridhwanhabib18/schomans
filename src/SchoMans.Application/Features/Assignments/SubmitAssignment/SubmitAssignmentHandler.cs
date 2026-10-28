using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.SubmitAssignment;

/// <summary>
/// A student hands in work. After the due date it is still accepted but marked late. Handing in again replaces the
/// earlier work (and its file) until the teacher has scored it.
/// </summary>
public sealed class SubmitAssignmentHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, ISchoolClock clock)
{
    public async Task<Result<SubmissionDto>> Handle(SubmitAssignmentCommand command, CancellationToken ct)
    {
        var studentId = await SchoolMembership.StudentIdAsync(db, currentUser, ct);
        if (studentId is null || !await AssignmentAccess.GivenToStudentAsync(db, command.AssignmentId, studentId.Value, ct))
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        var assignment = await db.Assignments.AsNoTracking().FirstAsync(a => a.Id == command.AssignmentId, ct);
        var fileKey = string.IsNullOrWhiteSpace(command.FileKey) ? null : command.FileKey.Trim();
        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == assignment.Id && s.StudentId == studentId, ct);
        if (submission?.IsGraded == true)
        {
            return Error.Conflict("assignments.already_graded", "Your work is already scored and cannot be replaced.");
        }

        if (fileKey is not null && fileKey != submission?.FileKey)
        {
            if (!AssignmentFiles.IsSubmissionKeyOf(fileKey, assignment.Id, studentId.Value))
            {
                return Error.Validation("files.invalid_key", "The file does not belong to your work.");
            }

            var fileProblem = await AssignmentFiles.VerifyAsync(storage, fileKey, AssignmentFileRules.SubmissionTypes, ct);
            if (fileProblem is not null)
            {
                return fileProblem;
            }
        }

        var now = clock.UtcNow;
        var oldFile = submission?.FileKey;
        if (submission is null)
        {
            submission = Submission.Create(assignment, studentId.Value, fileKey, command.FileName, command.TextBody, now);
            db.Submissions.Add(submission);
        }
        else
        {
            submission.Replace(assignment, fileKey, command.FileName ?? submission.FileName, command.TextBody, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("assignments.submit_conflict", "Your work was handed in from another device at the same time. Reload and check.");
        }

        if (oldFile is not null && oldFile != submission.FileKey)
        {
            await storage.DeleteAsync(FileBucket.Private, oldFile, ct);
        }

        return (await AssignmentLoader.SubmissionOfAsync(db, assignment.Id, studentId.Value, ct))!;
    }
}
