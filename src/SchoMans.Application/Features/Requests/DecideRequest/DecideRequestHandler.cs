using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Requests.DecideRequest;

/// <param name="Approve">True approves the step; false rejects the whole request, and then the note says why.</param>
public sealed record DecideRequestCommand(Guid Id, bool Approve, string? Note);

public sealed class DecideRequestValidator : AbstractValidator<DecideRequestCommand>
{
    public DecideRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve).WithMessage("Give the reason for the rejection.");
        RuleFor(x => x.Note).MaximumLength(Request.NoteMaxLength);
    }
}

/// <summary>
/// The approver whose turn it is approves or rejects with a note. An approval moves the request to the next step (whose
/// approvers are told) or, after the last step, approves it; for a certificate letter that issues the letter. The
/// requester is told when the request is closed.
/// </summary>
public sealed class DecideRequestHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    INotificationService notifications,
    ISchoolClock clock,
    IssueLetterHandler letters)
{
    private const int LetterAttempts = 3;

    public async Task<Result<RequestDetailDto>> Handle(DecideRequestCommand command, CancellationToken ct)
    {
        var request = await db.Requests.Include(r => r.Approvals).FirstOrDefaultAsync(r => r.Id == command.Id, ct);
        if (request is null)
        {
            return Error.NotFound("requests.not_found", "The request does not exist.");
        }

        if (!request.IsPending)
        {
            return Error.Conflict("requests.not_pending", "The request is already decided or withdrawn.");
        }

        var chain = await RequestSupport.ChainAsync(db, request.Type, ct);
        if (RequestSupport.RoleAtStep(chain, request.CurrentStep) is not { } role)
        {
            return Error.Conflict("requests.chain_changed", "The approval chain changed under this request; ask the admin staff.");
        }

        if (!await RequestSupport.IsApproverAsync(db, currentUser, role, request.StudentId, ct))
        {
            return Error.Forbidden("requests.not_your_turn", "This request does not wait for your decision.");
        }

        var decision = command.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        request.Decide(decision, currentUser.UserId, command.Note, chain.Count, clock.UtcNow);

        if (request.Status == RequestStatus.Approved && request.Type == RequestType.CertificateLetter)
        {
            var problem = await SaveWithLetterAsync(request, ct);
            if (problem is not null)
            {
                return problem;
            }
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        await NotifyAsync(request, chain, ct);
        return await RequestSupport.DetailAsync(db, currentUser, request.Id, ct);
    }

    /// <summary>The number of a letter is the next one of the year; two letters issued at once can clash on it, so it is tried again.</summary>
    private async Task<Error?> SaveWithLetterAsync(Request request, CancellationToken ct)
    {
        for (var attempt = 0; attempt < LetterAttempts; attempt++)
        {
            await letters.IssueAsync(request, currentUser.FullName, attempt, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return null;
            }
            catch (DbUpdateException) when (attempt < LetterAttempts - 1)
            {
                foreach (var entry in db.ChangeTracker.Entries<Letter>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
            catch (DbUpdateException)
            {
                return Error.Conflict("requests.letter_conflict", "The letter could not be numbered. Try again.");
            }
        }

        return Error.Conflict("requests.letter_conflict", "The letter could not be numbered. Try again.");
    }

    private async Task NotifyAsync(Request request, IReadOnlyList<ApproverRole> chain, CancellationToken ct)
    {
        if (request.IsPending)
        {
            if (RequestSupport.RoleAtStep(chain, request.CurrentStep) is { } next)
            {
                await NotificationSender.SendAsync(
                    db,
                    notifications,
                    await RequestSupport.ApproverUserIdsAsync(db, next, request.StudentId, ct),
                    NotificationType.RequestAwaitingApproval,
                    "A request waits for your decision",
                    $"Approved by {currentUser.FullName}.",
                    $"/requests/{request.Id}",
                    ct);
            }

            return;
        }

        var approved = request.Status == RequestStatus.Approved;
        await NotificationSender.SendAsync(
            db,
            notifications,
            [request.RequesterId],
            NotificationType.RequestDecided,
            approved ? "Your request was approved" : "Your request was rejected",
            approved ? "Open it to see the decisions" + (request.Type == RequestType.CertificateLetter ? " and download the letter." : ".") : request.Approvals.OrderBy(a => a.Step).Last().Notes ?? "Open it to read why.",
            $"/requests/{request.Id}",
            ct);
    }
}
