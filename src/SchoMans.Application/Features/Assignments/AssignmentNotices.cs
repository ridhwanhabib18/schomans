using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments;

/// <summary>The notifications of the assignment flow: a new assignment, a reminder the day before, a score.</summary>
internal static class AssignmentNotices
{
    public static string LinkFor(Guid assignmentId) => $"/assignments/{assignmentId}";

    /// <summary>The due date in school time, for example "30 Oct 2026 23:59".</summary>
    public static string DueText(ISchoolClock clock, DateTimeOffset dueAt) =>
        clock.ToSchoolDateTime(dueAt).ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The user accounts of the active students of the given classrooms.</summary>
    public static Task<List<Guid>> StudentUserIdsAsync(IAppDbContext db, IReadOnlyCollection<Guid> classroomIds, CancellationToken ct) =>
        db.Students.AsNoTracking()
            .Where(s => classroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active)
            .Select(s => s.UserId)
            .ToListAsync(ct);

    public static async Task NewAssignmentAsync(
        IAppDbContext db,
        INotificationService notifications,
        ISchoolClock clock,
        Guid assignmentId,
        string title,
        string subjectName,
        DateTimeOffset dueAt,
        IReadOnlyCollection<Guid> classroomIds,
        CancellationToken ct)
    {
        var recipients = await StudentUserIdsAsync(db, classroomIds, ct);
        await NotificationSender.SendAsync(
            db,
            notifications,
            recipients,
            NotificationType.AssignmentCreated,
            $"New assignment: {title}",
            $"{subjectName}, due {DueText(clock, dueAt)}",
            LinkFor(assignmentId),
            ct);
    }
}
