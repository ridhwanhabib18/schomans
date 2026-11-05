using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments.GradeSubmission;
using SchoMans.Application.Features.Rubrics;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.EssayFeedback;

// ---- one piece of work ----

public sealed record DraftFeedbackCommand(Guid SubmissionId);

/// <summary>The teacher of the assignment asks the AI for a draft on one piece of work. It counts as one of the teacher's daily AI calls.</summary>
public sealed class DraftFeedbackHandler(IAppDbContext db, ICurrentUser currentUser, EssayFeedbackDrafter drafter)
{
    public async Task<Result<FeedbackDraftDto>> Handle(DraftFeedbackCommand command, CancellationToken ct)
    {
        var submission = await FeedbackJson.OwnSubmissionAsync(db, currentUser, command.SubmissionId, ct);
        return submission.IsSuccess ? await drafter.DraftAsync(currentUser.UserId, submission.Value, bulk: false, ct) : submission.Error;
    }
}

public sealed record GetFeedbackDraftQuery(Guid SubmissionId);

public sealed class GetFeedbackDraftHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<FeedbackDraftDto>> Handle(GetFeedbackDraftQuery query, CancellationToken ct)
    {
        var submission = await FeedbackJson.OwnSubmissionAsync(db, currentUser, query.SubmissionId, ct);
        if (!submission.IsSuccess)
        {
            return submission.Error;
        }

        var draft = await db.FeedbackDrafts.AsNoTracking().FirstOrDefaultAsync(d => d.SubmissionId == query.SubmissionId, ct);
        return draft is null ? Error.NotFound("feedback.not_found", "There is no draft for this work.") : FeedbackJson.ToDto(draft);
    }
}

public sealed record CriterionEdit(int Points, string Reason);

/// <param name="Criteria">One entry per criterion of the draft, in the same order, with the points and reason the teacher wants.</param>
public sealed record AcceptFeedbackCommand(Guid SubmissionId, List<CriterionEdit> Criteria, string Suggestion);

public sealed class AcceptFeedbackValidator : AbstractValidator<AcceptFeedbackCommand>
{
    public AcceptFeedbackValidator()
    {
        RuleFor(x => x.Criteria).NotNull().Must(c => c is { Count: >= 1 and <= RubricJson.MaxCriteria });
        RuleForEach(x => x.Criteria).ChildRules(c =>
        {
            c.RuleFor(x => x.Points).GreaterThanOrEqualTo(0);
            c.RuleFor(x => x.Reason).NotEmpty().MaximumLength(FeedbackJson.ReasonMaxLength);
        });
        RuleFor(x => x.Suggestion).NotEmpty().MaximumLength(FeedbackDraft.SuggestionMaxLength);
    }
}

/// <summary>
/// The teacher accepts a draft, as it is or edited: the points become the score of the work and the reasons with the suggestion its
/// feedback, exactly as if the teacher had scored it by hand (the student is notified). The final score always comes from the teacher.
/// </summary>
public sealed class AcceptFeedbackHandler(IAppDbContext db, ICurrentUser currentUser, GradeSubmissionHandler grading)
{
    public async Task<Result<FeedbackDraftDto>> Handle(AcceptFeedbackCommand command, CancellationToken ct)
    {
        var submission = await FeedbackJson.OwnSubmissionAsync(db, currentUser, command.SubmissionId, ct);
        if (!submission.IsSuccess)
        {
            return submission.Error;
        }

        var draft = await db.FeedbackDrafts.FirstOrDefaultAsync(d => d.SubmissionId == command.SubmissionId, ct);
        if (draft is null)
        {
            return Error.NotFound("feedback.not_found", "There is no draft for this work.");
        }

        if (draft.Status == FeedbackDraftStatus.Accepted)
        {
            return Error.Conflict("feedback.already_accepted", "This draft is already accepted.");
        }

        var written = FeedbackJson.Read(draft.CriteriaJson);
        if (command.Criteria.Count != written.Count)
        {
            return Error.Validation("feedback.criteria_mismatch", "Give points and a reason for every criterion of the draft.");
        }

        var final = new List<CriterionScore>();
        for (var i = 0; i < written.Count; i++)
        {
            if (command.Criteria[i].Points > written[i].MaxPoints)
            {
                return Error.Validation("feedback.points_out_of_range", $"The points of '{written[i].Name}' are between 0 and {written[i].MaxPoints}.");
            }

            final.Add(written[i] with { Points = command.Criteria[i].Points, Reason = command.Criteria[i].Reason.Trim() });
        }

        var suggestion = command.Suggestion.Trim();
        var scored = await grading.Handle(new GradeSubmissionCommand(command.SubmissionId, FeedbackJson.ScoreOf(final), FeedbackJson.ComposeFeedback(final, suggestion)), ct);
        if (!scored.IsSuccess)
        {
            return scored.Error;
        }

        draft.Accept(FeedbackJson.Write(final), suggestion);
        await db.SaveChangesAsync(ct);
        return FeedbackJson.ToDto(draft);
    }
}

public sealed record RejectFeedbackCommand(Guid SubmissionId);

/// <summary>The teacher turns the draft down. The work stays unscored and the draft stays on record as rejected.</summary>
public sealed class RejectFeedbackHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<FeedbackDraftDto>> Handle(RejectFeedbackCommand command, CancellationToken ct)
    {
        var submission = await FeedbackJson.OwnSubmissionAsync(db, currentUser, command.SubmissionId, ct);
        if (!submission.IsSuccess)
        {
            return submission.Error;
        }

        var draft = await db.FeedbackDrafts.FirstOrDefaultAsync(d => d.SubmissionId == command.SubmissionId, ct);
        if (draft is null)
        {
            return Error.NotFound("feedback.not_found", "There is no draft for this work.");
        }

        if (draft.Status == FeedbackDraftStatus.Accepted)
        {
            return Error.Conflict("feedback.already_accepted", "An accepted draft cannot be rejected; correct the score of the work instead.");
        }

        draft.Reject();
        await db.SaveChangesAsync(ct);
        return FeedbackJson.ToDto(draft);
    }
}

// ---- a whole assignment ----

public sealed record DraftAllFeedbackCommand(Guid AssignmentId);

/// <param name="Drafted">New drafts written.</param>
/// <param name="AlreadyHadDraft">Work that already has a draft (accepted, rejected or waiting): a bulk run never overwrites the teacher's decisions.</param>
/// <param name="NotText">Work handed in as a file, which the AI cannot read.</param>
/// <param name="AlreadyScored">Work the teacher has already scored.</param>
/// <param name="Failed">Work whose draft the AI could not give (an unusable answer); try again for it.</param>
/// <param name="StoppedBy">The error code when the run stopped early (the daily limit or the month's budget), otherwise null.</param>
public sealed record DraftedFeedbackDto(int Drafted, int AlreadyHadDraft, int NotText, int AlreadyScored, int Failed, string? StoppedBy);

/// <summary>
/// Drafts the feedback of every piece of work of an assignment that has none yet. The first AI call counts as one of the teacher's
/// daily calls, the rest of the run does not (they still count toward the month's budget).
/// </summary>
public sealed class DraftAllFeedbackHandler(IAppDbContext db, ICurrentUser currentUser, EssayFeedbackDrafter drafter)
{
    public async Task<Result<DraftedFeedbackDto>> Handle(DraftAllFeedbackCommand command, CancellationToken ct)
    {
        var assignment = await RubricJson.OwnAssignmentAsync(db, currentUser, command.AssignmentId, ct);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        if (!await db.Rubrics.AnyAsync(r => r.AssignmentId == command.AssignmentId, ct))
        {
            return Error.Conflict(EssayFeedbackDrafter.NoRubricCode, "Give the assignment a rubric first; the AI judges against its criteria.");
        }

        var submissions = await db.Submissions.Include(s => s.Assignment).Where(s => s.AssignmentId == command.AssignmentId).OrderBy(s => s.SubmittedAt).ToListAsync(ct);
        var haveDraft = (await db.FeedbackDrafts.AsNoTracking().Where(d => d.Submission!.AssignmentId == command.AssignmentId).Select(d => d.SubmissionId).ToListAsync(ct)).ToHashSet();

        int drafted = 0, alreadyHadDraft = 0, notText = 0, alreadyScored = 0, failed = 0;
        string? stoppedBy = null;
        var firstCall = true;
        foreach (var submission in submissions)
        {
            if (submission.IsGraded)
            {
                alreadyScored++;
            }
            else if (haveDraft.Contains(submission.Id))
            {
                alreadyHadDraft++;
            }
            else if (string.IsNullOrWhiteSpace(submission.TextBody))
            {
                notText++;
            }
            else
            {
                var result = await drafter.DraftAsync(currentUser.UserId, submission, bulk: !firstCall, ct);
                if (result.IsSuccess)
                {
                    drafted++;
                    firstCall = false;
                }
                else if (result.Error.Code is "ai.budget_exhausted" or "ai.daily_limit")
                {
                    stoppedBy = result.Error.Code;
                    break;
                }
                else
                {
                    failed++;
                    firstCall = false;
                }
            }
        }

        return new DraftedFeedbackDto(drafted, alreadyHadDraft, notText, alreadyScored, failed, stoppedBy);
    }
}

public sealed record ListFeedbackDraftsQuery(Guid AssignmentId);

public sealed record FeedbackDraftSummaryDto(Guid SubmissionId, FeedbackDraftStatus Status, decimal Score);

/// <summary>Which pieces of work of an assignment have a draft, and where each stands; the teacher's list uses it to show the badges.</summary>
public sealed class ListFeedbackDraftsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<List<FeedbackDraftSummaryDto>>> Handle(ListFeedbackDraftsQuery query, CancellationToken ct)
    {
        var assignment = await RubricJson.OwnAssignmentAsync(db, currentUser, query.AssignmentId, ct);
        if (!assignment.IsSuccess)
        {
            return assignment.Error;
        }

        var drafts = await db.FeedbackDrafts.AsNoTracking().Where(d => d.Submission!.AssignmentId == query.AssignmentId).ToListAsync(ct);
        return drafts.Select(d => new FeedbackDraftSummaryDto(d.SubmissionId, d.Status, FeedbackJson.ScoreOf(FeedbackJson.Read(d.CriteriaJson)))).ToList();
    }
}
