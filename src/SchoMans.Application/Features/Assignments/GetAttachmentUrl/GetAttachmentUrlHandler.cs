using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.GetAttachmentUrl;

/// <param name="StudentId">For a parent: the child through whom they see the assignment.</param>
public sealed record GetAttachmentUrlQuery(Guid AssignmentId, Guid AttachmentId, Guid? StudentId = null);

/// <summary>A short-lived download address for an attachment, for everybody who may see the assignment.</summary>
public sealed class GetAttachmentUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<DownloadUrlDto>> Handle(GetAttachmentUrlQuery query, CancellationToken ct)
    {
        var assignment = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == query.AssignmentId)
            .Select(a => new { a.TeacherId, Key = a.Attachments.Where(f => f.Id == query.AttachmentId).Select(f => f.FileKey).FirstOrDefault() })
            .FirstOrDefaultAsync(ct);
        if (assignment?.Key is null)
        {
            return Error.NotFound("assignments.attachment_not_found", "The attachment does not exist.");
        }

        var viewer = await AssignmentAccess.ForAsync(db, currentUser, query.AssignmentId, assignment.TeacherId, query.StudentId, ct);
        if (!viewer.CanView)
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        var url = storage.CreateDownloadUrl(assignment.Key, AssignmentFiles.DownloadLifetime);
        return new DownloadUrlDto(url, timeProvider.GetUtcNow() + AssignmentFiles.DownloadLifetime);
    }
}
