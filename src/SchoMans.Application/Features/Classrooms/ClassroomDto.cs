using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Classrooms;

public sealed record ClassroomDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid HomeroomTeacherId,
    string HomeroomTeacherName,
    int StudentCount,
    string RowVersion);

internal sealed record ClassroomRow(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid HomeroomTeacherId,
    string HomeroomTeacherName,
    byte[] RowVersion);

internal static class ClassroomMapping
{
    public static readonly Expression<Func<Classroom, ClassroomRow>> Projection = c => new ClassroomRow(
        c.Id,
        c.Name,
        c.AcademicYearId,
        c.AcademicYear!.Name,
        c.HomeroomTeacherId,
        c.HomeroomTeacher!.User!.FullName,
        c.RowVersion);

    /// <summary>The number of active students per classroom, for a whole page in one query.</summary>
    public static async Task<Dictionary<Guid, int>> StudentCounts(IAppDbContext db, IReadOnlyCollection<Guid> classroomIds, CancellationToken ct) =>
        await db.Students.AsNoTracking()
            .Where(s => classroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active)
            .GroupBy(s => s.ClassroomId)
            .Select(g => new { ClassroomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassroomId, x => x.Count, ct);

    public static ClassroomDto ToDto(ClassroomRow row, int studentCount) => new(
        row.Id,
        row.Name,
        row.AcademicYearId,
        row.AcademicYearName,
        row.HomeroomTeacherId,
        row.HomeroomTeacherName,
        studentCount,
        RowVersionCodec.Encode(row.RowVersion));
}

internal static class ClassroomAccess
{
    public static bool CanRead(UserRole role) => role is UserRole.Admin or UserRole.Principal or UserRole.Teacher;
}
