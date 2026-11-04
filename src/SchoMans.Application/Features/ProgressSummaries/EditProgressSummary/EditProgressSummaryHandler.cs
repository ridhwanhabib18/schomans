using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.ProgressSummaries.EditProgressSummary;

/// <summary>The homeroom teacher rewrites a summary. It becomes a (human) draft again and must be approved before it can be sent.</summary>
public sealed class EditProgressSummaryHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProgressSummaryDto>> Handle(EditProgressSummaryCommand command, CancellationToken ct)
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

        if (!summary.Edit(command.Body))
        {
            return Error.Conflict("progress.already_sent", "This summary was already sent to the parents and can no longer change.");
        }

        await db.SaveChangesAsync(ct);
        return ProgressSummaryMapping.ToDto(summary, summary.Student.User!.FullName);
    }
}
