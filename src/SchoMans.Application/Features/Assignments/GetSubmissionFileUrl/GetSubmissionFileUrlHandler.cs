using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.GetSubmissionFileUrl;

public sealed record GetSubmissionFileUrlQuery(Guid SubmissionId);

/// <summary>A short-lived download address for the file of a piece of work. Files stay private: nobody gets a lasting link.</summary>
public sealed class GetSubmissionFileUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<DownloadUrlDto>> Handle(GetSubmissionFileUrlQuery query, CancellationToken ct)
    {
        var submission = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == query.SubmissionId)
            .Select(s => new { s.StudentId, s.Assignment!.TeacherId, s.FileKey })
            .FirstOrDefaultAsync(ct);
        if (submission is null)
        {
            return Error.NotFound("submissions.not_found", "The handed-in work does not exist.");
        }

        if (!await SubmissionAccess.CanViewAsync(db, currentUser, submission.StudentId, submission.TeacherId, ct))
        {
            return Error.Forbidden("submissions.forbidden", "You cannot open this work.");
        }

        if (submission.FileKey is null)
        {
            return Error.NotFound("submissions.no_file", "This work has no file.");
        }

        var url = storage.CreateDownloadUrl(submission.FileKey, AssignmentFiles.DownloadLifetime);
        return new DownloadUrlDto(url, timeProvider.GetUtcNow() + AssignmentFiles.DownloadLifetime);
    }
}
