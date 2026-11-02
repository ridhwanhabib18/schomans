using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;

namespace SchoMans.Application.Features.Requests.Chains;

public sealed record GetApprovalChainsQuery;

/// <param name="Roles">The roles in the order they decide, one to five steps.</param>
public sealed record SetApprovalChainCommand(RequestType Type, List<ApproverRole> Roles);

public sealed class SetApprovalChainValidator : AbstractValidator<SetApprovalChainCommand>
{
    public SetApprovalChainValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Roles).NotNull().Must(roles => roles is { Count: >= 1 and <= 5 }).WithMessage("A chain has between 1 and 5 steps.");
        RuleForEach(x => x.Roles).IsInEnum();
        RuleFor(x => x)
            .Must(x => x.Roles is null || ApprovalChainStep.IsValidChain(x.Type, x.Roles))
            .WithName("Roles")
            .WithMessage("A teacher's leave is not about a student, so it has no homeroom teacher step.");
    }
}

public sealed class GetApprovalChainsHandler(IAppDbContext db)
{
    public async Task<Result<List<ApprovalChainDto>>> Handle(GetApprovalChainsQuery query, CancellationToken ct) => await LoadAsync(db, ct);

    internal static async Task<List<ApprovalChainDto>> LoadAsync(IAppDbContext db, CancellationToken ct)
    {
        var chains = await RequestSupport.ChainsAsync(db, ct);
        var pending = await db.Requests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending).Select(r => r.Type).Distinct().ToListAsync(ct);
        return Enum.GetValues<RequestType>()
            .Select(type => new ApprovalChainDto(type, chains.GetValueOrDefault(type) ?? [], pending.Contains(type)))
            .ToList();
    }
}

/// <summary>
/// The admin staff set the chain of a request type. While requests of that type wait for a decision the chain cannot
/// change: their current step would point at a step that is no longer the same.
/// </summary>
public sealed class SetApprovalChainHandler(IAppDbContext db)
{
    public async Task<Result<List<ApprovalChainDto>>> Handle(SetApprovalChainCommand command, CancellationToken ct)
    {
        if (await db.Requests.AnyAsync(r => r.Type == command.Type && r.Status == RequestStatus.Pending, ct))
        {
            return Error.Conflict("requests.chain_in_use", "Requests of this kind are waiting for a decision, so the chain cannot change now.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // a retry must start from a clean slate
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Two saves: the old steps go first, or the unique index on the step number rejects the new ones.
            db.ApprovalChainSteps.RemoveRange(await db.ApprovalChainSteps.Where(s => s.RequestType == command.Type).ToListAsync(ct));
            await db.SaveChangesAsync(ct);

            for (var i = 0; i < command.Roles.Count; i++)
            {
                db.ApprovalChainSteps.Add(ApprovalChainStep.Create(command.Type, i + 1, command.Roles[i]));
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return await GetApprovalChainsHandler.LoadAsync(db, ct);
    }
}
