using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.ApproveProgressSummary;

/// <summary>The homeroom teacher has read the summary and accepts it for the bulk send. Approving twice changes nothing.</summary>
public sealed class ApproveProgressSummaryHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProgressSummaryDto>> Handle(ApproveProgressSummaryCommand command, CancellationToken ct)
    {
        var summary = await db.ProgressSummaries.Include(s => s.Student!.User).FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (summary is null)
        {
            return Error.NotFound("progress.not_found", "The summary does not exist.");
        }

        if (await ProgressSummaryAccess.RequireHomeroomAsync(db, currentUser, summary.Student!.ClassroomId, ct) is { } denied)
        {
            return denied;
        }

        if (summary.Status == ProgressSummaryStatus.Sent)
        {
            return Error.Conflict("progress.already_sent", "This summary was already sent to the parents.");
        }

        if (summary.Approve(currentUser.UserId))
        {
            await db.SaveChangesAsync(ct);
        }

        return ProgressSummaryMapping.ToDto(summary, summary.Student.User!.FullName);
    }
}
