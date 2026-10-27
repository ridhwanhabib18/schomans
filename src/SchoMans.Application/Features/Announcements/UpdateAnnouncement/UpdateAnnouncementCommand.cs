using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Announcements.UpdateAnnouncement;

/// <summary>
/// Edits an announcement. The title, body and cover image can always change; the audience (public flag, roles,
/// classrooms) only while it is a draft, because the notifications go out when it is published.
/// </summary>
public sealed record UpdateAnnouncementCommand(
    Guid Id,
    string Title,
    string Body,
    bool IsPublic,
    IReadOnlyList<UserRole> TargetRoles,
    IReadOnlyList<Guid> TargetClassroomIds,
    string? CoverImageKey,
    string RowVersion);
