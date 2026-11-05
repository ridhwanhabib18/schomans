using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.EssayFeedback;

/// <summary>The score of one criterion in a draft: the criterion as it was when the draft was written, the points and why.</summary>
public sealed record CriterionScore(string Name, int MaxPoints, int Points, string Reason);

/// <param name="Score">The points of all criteria on the scale of a score (0 to 100), the number that becomes the score of the work once accepted.</param>
public sealed record FeedbackDraftDto(Guid SubmissionId, FeedbackDraftStatus Status, IReadOnlyList<CriterionScore> Criteria, string Suggestion, decimal Score);

internal static class FeedbackJson
{
    public const int ReasonMaxLength = 180;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(IEnumerable<CriterionScore> criteria) => JsonSerializer.Serialize(criteria, Options);

    public static List<CriterionScore> Read(string json) => JsonSerializer.Deserialize<List<CriterionScore>>(json, Options) ?? [];

    /// <summary>The points of all criteria as a score from 0 to 100, with one decimal.</summary>
    public static decimal ScoreOf(IReadOnlyCollection<CriterionScore> criteria)
    {
        var max = criteria.Sum(c => c.MaxPoints);
        return max == 0 ? 0 : decimal.Round(criteria.Sum(c => c.Points) * 100m / max, 1);
    }

    public static FeedbackDraftDto ToDto(FeedbackDraft draft)
    {
        var criteria = Read(draft.CriteriaJson);
        return new FeedbackDraftDto(draft.SubmissionId, draft.Status, criteria, draft.Suggestion, ScoreOf(criteria));
    }

    /// <summary>The text that becomes the feedback of the work once the draft is accepted: a line per criterion, then the suggestion.</summary>
    public static string ComposeFeedback(IReadOnlyList<CriterionScore> criteria, string suggestion)
    {
        var lines = criteria.Select(c => $"{c.Name} ({c.Points}/{c.MaxPoints}): {c.Reason}");
        var text = string.Join('\n', lines) + $"\nSuggestion: {suggestion}";
        return text.Length <= Submission.FeedbackMaxLength ? text : text[..(Submission.FeedbackMaxLength - 1)] + "…";
    }

    /// <summary>The work, when it exists and its assignment was given by the signed-in teacher.</summary>
    public static async Task<Result<Submission>> OwnSubmissionAsync(IAppDbContext db, ICurrentUser currentUser, Guid submissionId, CancellationToken ct)
    {
        var submission = await db.Submissions.Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null)
        {
            return Error.NotFound("submissions.not_found", "The handed-in work does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != submission.Assignment!.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment can work with the feedback of its submissions.");
        }

        return submission;
    }
}
