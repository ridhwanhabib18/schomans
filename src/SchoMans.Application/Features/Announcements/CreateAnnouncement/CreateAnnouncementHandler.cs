using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.CreateAnnouncement;

public sealed class CreateAnnouncementHandler(IAppDbContext db, ICurrentUser currentUser, IHtmlContentSanitizer sanitizer, IFileStorage storage)
{
    public async Task<Result<AnnouncementDto>> Handle(CreateAnnouncementCommand command, CancellationToken ct)
    {
        var distinctClassrooms = command.TargetClassroomIds.Distinct().ToList();
        if (await db.Classrooms.CountAsync(c => distinctClassrooms.Contains(c.Id), ct) != distinctClassrooms.Count)
        {
            return Error.NotFound("classrooms.not_found", "One of the target classrooms does not exist.");
        }

        var announcement = Announcement.Create(
            command.Title,
            sanitizer.Sanitize(command.Body),
            command.IsPublic,
            command.TargetRoles,
            distinctClassrooms,
            currentUser.UserId);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);

        return AnnouncementMapping.ToDto(announcement, storage);
    }
}
