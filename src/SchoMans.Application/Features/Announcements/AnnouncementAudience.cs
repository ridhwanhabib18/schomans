using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Announcements;

/// <summary>
/// Who an announcement is for. Two questions share one definition: who gets a notification when it is published
/// (<see cref="RecipientUserIdsAsync"/>), and who may read it afterwards (<see cref="VisibleTo"/>).
/// Empty target roles mean every school role (not Applicant); empty target classrooms mean no restriction by classroom.
/// A classroom reaches its students, the parents of those students, and its homeroom teacher.
/// </summary>
internal static class AnnouncementAudience
{
    public static readonly UserRole[] SchoolRoles =
        [UserRole.Principal, UserRole.Teacher, UserRole.Student, UserRole.Parent, UserRole.Admin];

    /// <summary>The users to notify. Principal and admin are only notified by role (they oversee, not belong to classrooms).</summary>
    public static async Task<HashSet<Guid>> RecipientUserIdsAsync(IAppDbContext db, Announcement announcement, CancellationToken ct)
    {
        var roles = announcement.TargetRoles.Count > 0 ? announcement.TargetRoles.ToList() : SchoolRoles.ToList();
        var recipients = new HashSet<Guid>();

        if (announcement.TargetClassroomIds.Count == 0)
        {
            recipients.UnionWith(await db.Users.AsNoTracking().Where(u => roles.Contains(u.Role)).Select(u => u.Id).ToListAsync(ct));
            return recipients;
        }

        var classroomIds = announcement.TargetClassroomIds;
        if (roles.Contains(UserRole.Student))
        {
            recipients.UnionWith(await db.Students.AsNoTracking()
                .Where(s => classroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active)
                .Select(s => s.UserId)
                .ToListAsync(ct));
        }

        if (roles.Contains(UserRole.Parent))
        {
            recipients.UnionWith(await db.StudentParents.AsNoTracking()
                .Where(p => classroomIds.Contains(p.Student!.ClassroomId) && p.Student.Status == StudentStatus.Active)
                .Select(p => p.ParentUserId)
                .ToListAsync(ct));
        }

        if (roles.Contains(UserRole.Teacher))
        {
            recipients.UnionWith(await db.Classrooms.AsNoTracking()
                .Where(c => classroomIds.Contains(c.Id))
                .Select(c => c.HomeroomTeacher!.UserId)
                .ToListAsync(ct));
        }

        return recipients;
    }

    /// <summary>The classrooms the current user belongs to: a student's own, the children's of a parent, the homeroom classes of a teacher.</summary>
    public static async Task<List<Guid>> ClassroomIdsOfAsync(IAppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        return user.Role switch
        {
            UserRole.Student => await db.Students.AsNoTracking()
                .Where(s => s.UserId == user.UserId && s.Status == StudentStatus.Active)
                .Select(s => s.ClassroomId)
                .ToListAsync(ct),
            UserRole.Parent => await db.StudentParents.AsNoTracking()
                .Where(p => p.ParentUserId == user.UserId && p.Student!.Status == StudentStatus.Active)
                .Select(p => p.Student!.ClassroomId)
                .Distinct()
                .ToListAsync(ct),
            UserRole.Teacher => await db.Classrooms.AsNoTracking()
                .Where(c => c.HomeroomTeacher!.UserId == user.UserId)
                .Select(c => c.Id)
                .ToListAsync(ct),
            _ => [],
        };
    }

    /// <summary>Announcements a user of this role (belonging to these classrooms) may read. Principal and admin read all of them.</summary>
    public static Expression<Func<Announcement, bool>> VisibleTo(UserRole role, IReadOnlyCollection<Guid> classroomIds)
    {
        if (role is UserRole.Admin or UserRole.Principal)
        {
            return a => true;
        }

        var isSchoolRole = SchoolRoles.Contains(role);
        var ids = classroomIds.ToList();
        return a =>
            (a.TargetRoles.Count == 0 ? isSchoolRole : a.TargetRoles.Contains(role))
            && (a.TargetClassroomIds.Count == 0 || a.TargetClassroomIds.Any(id => ids.Contains(id)));
    }
}
