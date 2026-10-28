using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Communication;

/// <summary>
/// An announcement for the people inside the school, targeted at roles and/or classrooms. It is internal unless
/// <see cref="IsPublic"/> is set, which also shows it to guests. The body is already sanitized HTML.
/// </summary>
public sealed class Announcement : BaseEntity
{
    private Announcement()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public bool IsPublic { get; private set; }

    public string? CoverImageKey { get; private set; }

    /// <summary>Empty means every school role (not Applicant).</summary>
    public List<UserRole> TargetRoles { get; private set; } = [];

    /// <summary>Empty means no restriction by classroom.</summary>
    public List<Guid> TargetClassroomIds { get; private set; } = [];

    public PublishStatus Status { get; private set; } = PublishStatus.Draft;

    public DateTimeOffset? PublishedAt { get; private set; }

    public Guid CreatedById { get; private set; }

    public static Announcement Create(
        string title,
        string body,
        bool isPublic,
        IEnumerable<UserRole> targetRoles,
        IEnumerable<Guid> targetClassroomIds,
        Guid createdById)
    {
        var announcement = new Announcement { CreatedById = createdById };
        announcement.UpdateContent(title, body);
        announcement.SetTargets(isPublic, targetRoles, targetClassroomIds);
        return announcement;
    }

    public void UpdateContent(string title, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(body);

        Title = title.Trim();
        Body = body;
    }

    /// <summary>
    /// Changes who gets the announcement. Only possible for a draft: once it is published the notifications have gone out,
    /// so the audience is fixed. Returns false for a published announcement.
    /// </summary>
    public bool UpdateTargets(bool isPublic, IEnumerable<UserRole> targetRoles, IEnumerable<Guid> targetClassroomIds)
    {
        if (Status == PublishStatus.Published)
        {
            return false;
        }

        SetTargets(isPublic, targetRoles, targetClassroomIds);
        return true;
    }

    public void SetCover(string? coverImageKey) =>
        CoverImageKey = string.IsNullOrWhiteSpace(coverImageKey) ? null : coverImageKey;

    /// <summary>Publishes a draft. Returns false when it is already published.</summary>
    public bool Publish(DateTimeOffset now)
    {
        if (Status == PublishStatus.Published)
        {
            return false;
        }

        Status = PublishStatus.Published;
        PublishedAt = now;
        return true;
    }

    private void SetTargets(bool isPublic, IEnumerable<UserRole> targetRoles, IEnumerable<Guid> targetClassroomIds)
    {
        IsPublic = isPublic;
        TargetRoles = targetRoles.Distinct().ToList();
        TargetClassroomIds = targetClassroomIds.Distinct().ToList();
    }
}
