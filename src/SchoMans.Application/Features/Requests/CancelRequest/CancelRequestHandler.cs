using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Requests.CancelRequest;

public sealed record CancelRequestCommand(Guid Id);

/// <summary>The requester withdraws a request that nobody has decided on completely yet.</summary>
public sealed class CancelRequestHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<RequestDetailDto>> Handle(CancelRequestCommand command, CancellationToken ct)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == command.Id, ct);
        if (request is null)
        {
            return Error.NotFound("requests.not_found", "The request does not exist.");
        }

        if (request.RequesterId != currentUser.UserId)
        {
            return Error.Forbidden("requests.not_yours", "Only the person who made the request can withdraw it.");
        }

        if (!request.Cancel(clock.UtcNow))
        {
            return Error.Conflict("requests.not_pending", "The request is already decided or withdrawn.");
        }

        await db.SaveChangesAsync(ct);
        return await RequestSupport.DetailAsync(db, currentUser, request.Id, ct);
    }
}
