namespace SchoMans.Application.Features.Announcements.PublishAnnouncement;

public sealed record PublishAnnouncementCommand(Guid Id);

/// <param name="NotifiedUsers">How many users got an in-app notification.</param>
public sealed record PublishAnnouncementSummary(AnnouncementDto Announcement, int NotifiedUsers);
