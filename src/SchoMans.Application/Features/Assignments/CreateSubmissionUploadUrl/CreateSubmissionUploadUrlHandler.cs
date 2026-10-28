using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files.CreateUploadUrl;

namespace SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;

public sealed class CreateSubmissionUploadUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage)
{
    public async Task<Result<UploadUrlDto>> Handle(CreateSubmissionUploadUrlCommand command, CancellationToken ct)
    {
        var studentId = await SchoolMembership.StudentIdAsync(db, currentUser, ct);
        if (studentId is null || !await AssignmentAccess.GivenToStudentAsync(db, command.AssignmentId, studentId.Value, ct))
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        if (await db.Submissions.AnyAsync(s => s.AssignmentId == command.AssignmentId && s.StudentId == studentId && s.GradedAt != null, ct))
        {
            return Error.Conflict("assignments.already_graded", "Your work is already scored and cannot be replaced.");
        }

        var key = AssignmentFiles.NewSubmissionKey(command.AssignmentId, studentId.Value, command.ContentType);
        var upload = await storage.CreateUploadUrlAsync(FileBucket.Private, key, command.ContentType, AssignmentFiles.UploadLifetime, ct);
        return new UploadUrlDto(upload.Url, key, command.ContentType, upload.ExpiresAt);
    }
}
