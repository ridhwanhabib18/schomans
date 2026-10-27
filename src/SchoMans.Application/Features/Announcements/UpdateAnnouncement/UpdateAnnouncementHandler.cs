using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files;

namespace SchoMans.Application.Features.Announcements.UpdateAnnouncement;

public sealed class UpdateAnnouncementHandler(IAppDbContext db, IHtmlContentSanitizer sanitizer, IFileStorage storage)
{
    public async Task<Result<AnnouncementDto>> Handle(UpdateAnnouncementCommand command, CancellationToken ct)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (announcement is null)
        {
            return Error.NotFound("announcements.not_found", "The announcement does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var classroomIds = command.TargetClassroomIds.Distinct().ToList();
        if (await db.Classrooms.CountAsync(c => classroomIds.Contains(c.Id), ct) != classroomIds.Count)
        {
            return Error.NotFound("classrooms.not_found", "One of the target classrooms does not exist.");
        }

        var roles = command.TargetRoles.Distinct().ToList();
        var audienceChanged = announcement.IsPublic != command.IsPublic
            || !announcement.TargetRoles.ToHashSet().SetEquals(roles)
            || !announcement.TargetClassroomIds.ToHashSet().SetEquals(classroomIds);
        if (audienceChanged && !announcement.UpdateTargets(command.IsPublic, roles, classroomIds))
        {
            return Error.Conflict(
                "announcements.audience_locked",
                "The audience of a published announcement cannot change, because the notifications have already been sent.");
        }

        var coverKey = string.IsNullOrWhiteSpace(command.CoverImageKey) ? null : command.CoverImageKey.Trim();
        var coverChanged = coverKey != announcement.CoverImageKey;
        if (coverChanged && await CoverImages.VerifyAsync(storage, CoverImages.AnnouncementsModule, announcement.Id, coverKey, ct) is { } coverError)
        {
            return coverError;
        }

        var previousCover = announcement.CoverImageKey;
        announcement.UpdateContent(command.Title, sanitizer.Sanitize(command.Body));
        announcement.SetCover(coverKey);
        db.SetExpectedRowVersion(announcement, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }

        if (coverChanged && previousCover is not null)
        {
            try
            {
                await storage.DeleteAsync(FileBucket.Private, previousCover, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Housekeeping only: an orphaned file must not fail the save.
            }
        }

        return AnnouncementMapping.ToDto(announcement, storage);
    }
}
