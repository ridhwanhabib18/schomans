using SchoMans.Domain.Common;

namespace SchoMans.Domain.Assignments;

public enum FeedbackDraftStatus
{
    /// <summary>Written by the AI, waiting for the teacher.</summary>
    Draft,

    /// <summary>The teacher accepted it (possibly after editing): the score and the feedback are on the submission.</summary>
    Accepted,

    /// <summary>The teacher turned it down; the work is still unscored.</summary>
    Rejected,
}

/// <summary>
/// The AI's draft feedback on one piece of work (AI-05): a score and a reason per criterion and one suggestion for the student.
/// It never scores the work by itself; only an accepted draft becomes the score and the feedback of the submission.
/// </summary>
public sealed class FeedbackDraft : BaseEntity
{
    public const int SuggestionMaxLength = 300;
    public const int CriteriaJsonMaxLength = 6_000;

    private FeedbackDraft()
    {
    }

    public Guid SubmissionId { get; private set; }

    public Submission? Submission { get; private set; }

    public FeedbackDraftStatus Status { get; private set; }

    /// <summary>The criteria as they were when the draft was written, each with its points and reason (a snapshot: a later rubric edit does not change it).</summary>
    public string CriteriaJson { get; private set; } = "[]";

    public string Suggestion { get; private set; } = string.Empty;

    public static FeedbackDraft Create(Guid submissionId, string criteriaJson, string suggestion)
    {
        var draft = new FeedbackDraft { SubmissionId = submissionId };
        draft.Redraft(criteriaJson, suggestion);
        return draft;
    }

    /// <summary>Replaces the content with a new AI draft; the teacher has to look at it again.</summary>
    public void Redraft(string criteriaJson, string suggestion)
    {
        Fill(criteriaJson, suggestion);
        Status = FeedbackDraftStatus.Draft;
    }

    /// <summary>The teacher's accepted version, which may differ from the draft.</summary>
    public void Accept(string criteriaJson, string suggestion)
    {
        Fill(criteriaJson, suggestion);
        Status = FeedbackDraftStatus.Accepted;
    }

    public void Reject() => Status = FeedbackDraftStatus.Rejected;

    private void Fill(string criteriaJson, string suggestion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(criteriaJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(criteriaJson.Length, CriteriaJsonMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(suggestion.Trim().Length, SuggestionMaxLength);

        CriteriaJson = criteriaJson;
        Suggestion = suggestion.Trim();
    }
}
