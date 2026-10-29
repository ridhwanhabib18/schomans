using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Requests.GetRequest;

public sealed record GetRequestQuery(Guid Id);

/// <summary>A request with its chain, the decisions so far and, for the one who has to decide now, the right to do so.</summary>
public sealed class GetRequestHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<RequestDetailDto>> Handle(GetRequestQuery query, CancellationToken ct)
    {
        var request = await db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == query.Id, ct);
        if (request is null)
        {
            return Error.NotFound("requests.not_found", "The request does not exist.");
        }

        if (!await RequestSupport.CanViewAsync(db, currentUser, request, ct))
        {
            return Error.Forbidden("requests.not_yours", "You may not open this request.");
        }

        return await RequestSupport.DetailAsync(db, currentUser, request.Id, ct);
    }
}
