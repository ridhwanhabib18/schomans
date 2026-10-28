using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments.SendDueReminders;

/// <summary>
/// Run every morning: students who have not handed in an assignment that is due tomorrow (school date) get a reminder.
/// Running it twice the same day sends nothing new: a student gets one reminder per assignment.
/// </summary>
public sealed class SendDueRemindersHandler(IAppDbContext db, INotificationService notifications, ISchoolClock clock)
{
    /// <returns>How many reminders were sent.</returns>
    public async Task<int> Handle(CancellationToken ct)
    {
        var tomorrow = clock.Today.AddDays(1);
        var from = clock.ToUtc(tomorrow, TimeOnly.MinValue);
        var until = clock.ToUtc(tomorrow.AddDays(1), TimeOnly.MinValue);

        var assignments = await db.Assignments.AsNoTracking()
            .Where(a => a.DueAt >= from && a.DueAt < until)
            .Select(a => new { a.Id, a.Title, a.DueAt, SubjectName = a.Subject!.Name, ClassroomIds = a.Classrooms.Select(c => c.ClassroomId).ToList() })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var assignment in assignments)
        {
            var link = AssignmentNotices.LinkFor(assignment.Id);
            var recipients = await db.Students.AsNoTracking()
                .Where(s => assignment.ClassroomIds.Contains(s.ClassroomId)
                    && s.Status == StudentStatus.Active
                    && !db.Submissions.Any(sub => sub.AssignmentId == assignment.Id && sub.StudentId == s.Id)
                    && !db.Notifications.Any(n => n.UserId == s.UserId && n.Type == NotificationType.AssignmentDueSoon && n.LinkUrl == link))
                .Select(s => s.UserId)
                .ToListAsync(ct);

            sent += await NotificationSender.SendAsync(
                db,
                notifications,
                recipients,
                NotificationType.AssignmentDueSoon,
                $"Due tomorrow: {assignment.Title}",
                $"{assignment.SubjectName}, due {AssignmentNotices.DueText(clock, assignment.DueAt)}. You have not handed it in yet.",
                link,
                ct);
        }

        return sent;
    }
}
