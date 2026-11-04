using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>The text of a report card that describes what a student achieved in a subject. Written by the teacher, or drafted by the AI and accepted by the teacher.</summary>
public sealed class AchievementDescription : BaseEntity
{
    public const int BodyMaxLength = 2_000;

    private AchievementDescription()
    {
    }

    public Guid ReportCardId { get; private set; }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public AchievementStatus Status { get; private set; } = AchievementStatus.Draft;

    public bool IsAiDrafted { get; private set; }

    public static AchievementDescription Create(Guid reportCardId, Guid subjectId, string body)
    {
        var description = new AchievementDescription { ReportCardId = reportCardId, SubjectId = subjectId };
        description.Write(body);
        return description;
    }

    /// <summary>A teacher writing or editing the text makes it a draft again until it is accepted, and it is no longer the AI's text.</summary>
    public void Write(string body)
    {
        SetBody(body);
        IsAiDrafted = false;
    }

    /// <summary>The AI's text replaces the body and waits for the teacher's review; the report card only shows accepted texts.</summary>
    public void Redraft(string body)
    {
        SetBody(body);
        IsAiDrafted = true;
    }

    public void Accept() => Status = AchievementStatus.Accepted;

    private void SetBody(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (body.Trim().Length > BodyMaxLength)
        {
            throw new ArgumentException($"A description is at most {BodyMaxLength} characters.", nameof(body));
        }

        Body = body.Trim();
        Status = AchievementStatus.Draft;
    }
}
