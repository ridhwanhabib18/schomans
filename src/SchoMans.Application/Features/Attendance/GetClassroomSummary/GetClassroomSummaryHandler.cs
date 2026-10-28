using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.GetClassroomSummary;

/// <summary>
/// A month of attendance for one classroom: per student how often present, excused, sick and absent, over every lesson
/// that had attendance taken. Admin staff and the principal see every classroom, a teacher the classrooms they work in.
/// </summary>
public sealed class GetClassroomSummaryHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ClassroomAttendanceSummaryDto>> Handle(GetClassroomSummaryQuery query, CancellationToken ct)
    {
        AttendanceMonth.TryParse(query.Month, out var first, out var last);

        var classroom = await db.Classrooms.AsNoTracking().Where(c => c.Id == query.ClassroomId).Select(c => new { c.Id, c.Name }).FirstOrDefaultAsync(ct);
        if (classroom is null)
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        if (!await CanReadAsync(classroom.Id, ct))
        {
            return Error.Forbidden("attendance.not_your_classroom", "You can only see attendance of classrooms you work in.");
        }

        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session!.Schedule!.ClassroomId == classroom.Id && r.Session.Date >= first && r.Session.Date <= last)
            .Select(r => new { r.StudentId, r.SessionId, r.Status })
            .ToListAsync(ct);

        // Everybody in the classroom now, plus anybody who has records for the month (moved or graduated since).
        var studentIds = records.Select(r => r.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking()
            .Where(s => (s.ClassroomId == classroom.Id && s.Status == StudentStatus.Active) || studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Nisn, s.User!.FullName })
            .ToListAsync(ct);

        var byStudent = records.ToLookup(r => r.StudentId, r => r.Status);
        var rows = students
            .OrderBy(s => s.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s.Nisn)
            .Select(s => new StudentAttendanceRowDto(s.Id, s.FullName, s.Nisn, AttendanceCountsDto.From(byStudent[s.Id].ToList())))
            .ToList();

        return new ClassroomAttendanceSummaryDto(classroom.Id, classroom.Name, query.Month, records.Select(r => r.SessionId).Distinct().Count(), rows);
    }

    private async Task<bool> CanReadAsync(Guid classroomId, CancellationToken ct)
    {
        if (currentUser.Role is UserRole.Admin or UserRole.Principal)
        {
            return true;
        }

        var teacherId = currentUser.Role == UserRole.Teacher ? await SchoolMembership.TeacherIdAsync(db, currentUser, ct) : null;
        return teacherId is not null && await SchoolMembership.TeacherClassroomIds(db, teacherId.Value).ContainsAsync(classroomId, ct);
    }
}
