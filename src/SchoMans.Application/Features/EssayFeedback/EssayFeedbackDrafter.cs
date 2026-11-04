using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.Rubrics;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.EssayFeedback;

/// <summary>
/// Drafts the feedback on one piece of work (AI-05): reads the rubric and the text, asks the AI through the gateway (the student's
/// name replaced by a code), checks the answer against the rubric, and stores it as a draft that the teacher still has to accept.
/// The caller has already checked that the signed-in teacher gave the assignment.
/// </summary>
public sealed class EssayFeedbackDrafter(IAppDbContext db, AiGateway gateway)
{
    public const string NoRubricCode = "feedback.no_rubric";
    public const string NeedsTextCode = "feedback.needs_text";
    public const string AlreadyScoredCode = "feedback.already_scored";

    /// <param name="bulk">A further call of a bulk run: it does not use up another of the teacher's daily calls.</param>
    public async Task<Result<FeedbackDraftDto>> DraftAsync(Guid userId, Submission submission, bool bulk, CancellationToken ct)
    {
        var assignment = submission.Assignment!;
        if (submission.IsGraded)
        {
            return Error.Conflict(AlreadyScoredCode, "This work is already scored; there is nothing left to draft.");
        }

        if (string.IsNullOrWhiteSpace(submission.TextBody))
        {
            return Error.Conflict(NeedsTextCode, "Only work handed in as text can be read by the AI; this one is a file.");
        }

        var rubricJson = await db.Rubrics.AsNoTracking().Where(r => r.AssignmentId == assignment.Id).Select(r => r.CriteriaJson).FirstOrDefaultAsync(ct);
        var rubric = rubricJson is null ? [] : RubricJson.Read(rubricJson);
        if (rubric.Count == 0)
        {
            return Error.Conflict(NoRubricCode, "Give the assignment a rubric first; the AI judges against its criteria.");
        }

        var student = await db.Students.AsNoTracking().Where(s => s.Id == submission.StudentId).Select(s => new { s.Id, Name = s.User!.FullName }).FirstAsync(ct);
        var aliases = new NameAliases();
        var request = FeedbackPrompt.Build(assignment.Title, assignment.Description, rubric, aliases.For(student.Id, student.Name), aliases.Scrub(submission.TextBody));

        var completion = await gateway.RunAsync(userId, request, ct, bulk);
        if (!completion.IsSuccess)
        {
            return completion.Error;
        }

        var parsed = FeedbackPrompt.Parse(completion.Value.Text, rubric);
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var (criteria, suggestion) = parsed.Value;
        var json = FeedbackJson.Write(criteria);
        var draft = await db.FeedbackDrafts.FirstOrDefaultAsync(d => d.SubmissionId == submission.Id, ct);
        if (draft is null)
        {
            draft = FeedbackDraft.Create(submission.Id, json, suggestion);
            db.FeedbackDrafts.Add(draft);
        }
        else
        {
            draft.Redraft(json, suggestion);
        }

        await db.SaveChangesAsync(ct);
        return FeedbackJson.ToDto(draft);
    }
}
