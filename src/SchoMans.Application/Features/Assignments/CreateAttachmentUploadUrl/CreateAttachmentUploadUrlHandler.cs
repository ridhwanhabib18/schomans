using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files.CreateUploadUrl;

namespace SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;

public sealed class CreateAttachmentUploadUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage)
{
    public async Task<Result<UploadUrlDto>> Handle(CreateAttachmentUploadUrlCommand command, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("assignments.not_a_teacher", "Only teachers give assignments.");
        }

        var key = AssignmentFiles.NewAttachmentKey(teacherId.Value, command.ContentType);
        var upload = await storage.CreateUploadUrlAsync(FileBucket.Private, key, command.ContentType, AssignmentFiles.UploadLifetime, ct);
        return new UploadUrlDto(upload.Url, key, command.ContentType, upload.ExpiresAt);
    }
}
