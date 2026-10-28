using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.GetStudentAttendance;

/// <summary>One student's attendance for a month, lesson by lesson. For the student, their parents, and the school staff of their classroom.</summary>
public sealed class GetStudentAttendanceHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<StudentAttendanceDto>> Handle(GetStudentAttendanceQuery query, CancellationToken ct)
    {
        AttendanceMonth.TryParse(query.Month, out var first, out var last);

        var studentId = query.StudentId;
        if (studentId is null)
        {
            if (currentUser.Role != UserRole.Student)
            {
                return Error.Validation("attendance.student_required", "Choose a student.");
            }

            studentId = await SchoolMembership.StudentIdAsync(db, currentUser, ct);
            if (studentId is null)
            {
                return Error.NotFound("students.not_found", "There is no student record for this account.");
            }
        }

        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.User!.FullName, ClassroomName = s.Classroom!.Name })
            .FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return Error.NotFound("students.not_found", "The student does not exist.");
        }

        if (!await SchoolMembership.CanSeeStudentAsync(db, currentUser, student.Id, ct))
        {
            return Error.Forbidden("attendance.not_your_student", "You cannot see this student's attendance.");
        }

        var entries = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == student.Id && r.Session!.Date >= first && r.Session.Date <= last)
            .OrderBy(r => r.Session!.Date)
            .ThenBy(r => r.Session!.Schedule!.StartTime)
            .Select(r => new StudentAttendanceEntryDto(
                r.SessionId,
                r.Session!.Date,
                r.Session.Schedule!.Subject!.Name,
                r.Session.Schedule.StartTime,
                r.Status,
                r.ScannedAt,
                r.Note))
            .ToListAsync(ct);

        return new StudentAttendanceDto(
            student.Id,
            student.FullName,
            student.ClassroomName,
            query.Month,
            AttendanceCountsDto.From(entries.Select(e => e.Status).ToList()),
            entries);
    }
}
