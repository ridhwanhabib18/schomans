using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Announcements.CreateAnnouncement;

/// <summary>Creates a draft. The body is rich-text HTML; it is sanitized before it is stored.</summary>
/// <param name="IsPublic">Also shows the announcement to guests on the public pages.</param>
/// <param name="TargetRoles">Empty means every school role.</param>
/// <param name="TargetClassroomIds">Empty means no restriction by classroom.</param>
public sealed record CreateAnnouncementCommand(
    string Title,
    string Body,
    bool IsPublic,
    IReadOnlyList<UserRole> TargetRoles,
    IReadOnlyList<Guid> TargetClassroomIds);
