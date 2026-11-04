using System.Globalization;
using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Communication;

/// <summary>
/// The monthly letter to the parents about one student (AI-01). The system drafts it (Draft), the homeroom teacher
/// reviews and edits it (Approved) and sends it in bulk (Sent). Only a Sent summary is visible to the family.
/// </summary>
public sealed class ProgressSummary : BaseEntity
{
    public const int BodyMaxLength = 3_000;

    private ProgressSummary()
    {
    }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    /// <summary>The month the summary is about, as <c>yyyy-MM</c>.</summary>
    public string Month { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public ProgressSummaryStatus Status { get; private set; } = ProgressSummaryStatus.Draft;

    /// <summary>True while the text is what the AI wrote; an edit by the teacher clears it.</summary>
    public bool IsAiDrafted { get; private set; }

    public Guid? ApprovedById { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static string MonthKey(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static bool IsMonthKey(string? month) =>
        month is { Length: 7 } && DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static ProgressSummary Draft(Guid studentId, string month, string body, bool isAiDrafted)
    {
        if (!IsMonthKey(month))
        {
            throw new ArgumentException("A month looks like 2026-10.", nameof(month));
        }

        var summary = new ProgressSummary { StudentId = studentId, Month = month, IsAiDrafted = isAiDrafted };
        summary.SetBody(body);
        return summary;
    }

    /// <summary>The teacher edits the text. It must be approved again; a sent summary cannot change. Returns false then.</summary>
    public bool Edit(string body)
    {
        if (Status == ProgressSummaryStatus.Sent)
        {
            return false;
        }

        SetBody(body);
        IsAiDrafted = false;
        Status = ProgressSummaryStatus.Draft;
        ApprovedById = null;
        return true;
    }

    /// <summary>A new AI draft replaces the text (the teacher asked for another one). A sent summary cannot change.</summary>
    public bool Redraft(string body)
    {
        if (Status == ProgressSummaryStatus.Sent)
        {
            return false;
        }

        SetBody(body);
        IsAiDrafted = true;
        Status = ProgressSummaryStatus.Draft;
        ApprovedById = null;
        return true;
    }

    /// <summary>The teacher has read the text and accepts it for sending. Only a draft can be approved.</summary>
    public bool Approve(Guid approverId)
    {
        if (Status != ProgressSummaryStatus.Draft)
        {
            return false;
        }

        Status = ProgressSummaryStatus.Approved;
        ApprovedById = approverId;
        return true;
    }

    /// <summary>Hands the approved summary to the family. Only an approved summary can be sent.</summary>
    public bool Send(DateTimeOffset now)
    {
        if (Status != ProgressSummaryStatus.Approved)
        {
            return false;
        }

        Status = ProgressSummaryStatus.Sent;
        SentAt = now;
        return true;
    }

    private void SetBody(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (body.Trim().Length > BodyMaxLength)
        {
            throw new ArgumentException($"A summary has at most {BodyMaxLength} characters.", nameof(body));
        }

        Body = body.Trim();
    }
}
