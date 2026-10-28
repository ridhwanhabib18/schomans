using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Schedules;

public sealed record ScheduleDto(
    Guid Id,
    Guid ClassroomId,
    string ClassroomName,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherName,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string RowVersion);

internal static class ScheduleMapping
{
    public static readonly Expression<Func<Schedule, ScheduleRow>> Projection = s => new ScheduleRow(
        s.Id,
        s.ClassroomId,
        s.Classroom!.Name,
        s.SubjectId,
        s.Subject!.Name,
        s.TeacherId,
        s.Teacher!.User!.FullName,
        s.DayOfWeek,
        s.StartTime,
        s.EndTime,
        s.RowVersion);

    public static ScheduleDto ToDto(ScheduleRow row) => new(
        row.Id,
        row.ClassroomId,
        row.ClassroomName,
        row.SubjectId,
        row.SubjectName,
        row.TeacherId,
        row.TeacherName,
        row.DayOfWeek,
        row.StartTime,
        row.EndTime,
        RowVersionCodec.Encode(row.RowVersion));

    public static async Task<ScheduleDto> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        ToDto(await db.Schedules.AsNoTracking().Where(s => s.Id == id).Select(Projection).FirstAsync(ct));
}

internal sealed record ScheduleRow(
    Guid Id,
    Guid ClassroomId,
    string ClassroomName,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherName,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    byte[] RowVersion);

/// <summary>The checks shared by create and update: the references exist, and neither the classroom nor the teacher is double-booked.</summary>
internal static class ScheduleRules
{
    public static async Task<Error?> CheckAsync(
        IAppDbContext db,
        Guid? scheduleId,
        Guid classroomId,
        Guid subjectId,
        Guid teacherId,
        DayOfWeek day,
        TimeOnly start,
        TimeOnly end,
        CancellationToken ct)
    {
        if (!await db.Classrooms.AnyAsync(c => c.Id == classroomId, ct))
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct))
        {
            return Error.NotFound("subjects.not_found", "The subject does not exist.");
        }

        if (!await db.Teachers.AnyAsync(t => t.Id == teacherId, ct))
        {
            return Error.NotFound("teachers.not_found", "The teacher does not exist.");
        }

        // Overlap: the other lesson starts before this one ends and ends after this one starts (touching ends are fine).
        var others = db.Schedules.Where(s => s.Id != scheduleId && s.DayOfWeek == day && s.StartTime < end && start < s.EndTime);
        if (await others.AnyAsync(s => s.ClassroomId == classroomId, ct))
        {
            return Error.Conflict("schedules.classroom_busy", "The classroom already has a lesson at this time.");
        }

        if (await others.AnyAsync(s => s.TeacherId == teacherId, ct))
        {
            return Error.Conflict("schedules.teacher_busy", "The teacher already teaches another lesson at this time.");
        }

        return null;
    }
}
