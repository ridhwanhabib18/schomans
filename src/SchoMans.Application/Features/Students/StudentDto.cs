using System.Linq.Expressions;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students;

public sealed record StudentDto(
    Guid Id,
    string Nisn,
    string FullName,
    string Email,
    DateOnly DateOfBirth,
    Guid ClassroomId,
    string ClassroomName,
    string Status,
    bool HasAccount,
    string RowVersion);

internal static class StudentMapping
{
    // EF translates this to SQL; the row version is encoded after the query, so the DTO is built in two steps.
    public static readonly Expression<Func<Student, StudentRow>> Projection = s => new StudentRow(
        s.Id,
        s.Nisn,
        s.User!.FullName,
        s.User.Email,
        s.DateOfBirth,
        s.ClassroomId,
        s.Classroom!.Name,
        s.Status,
        s.User.KeycloakId != null,
        s.RowVersion);

    public static StudentDto ToDto(StudentRow row) => new(
        row.Id,
        row.Nisn,
        row.FullName,
        row.Email,
        row.DateOfBirth,
        row.ClassroomId,
        row.ClassroomName,
        row.Status.ToString(),
        row.HasAccount,
        RowVersionCodec.Encode(row.RowVersion));
}

internal sealed record StudentRow(
    Guid Id,
    string Nisn,
    string FullName,
    string Email,
    DateOnly DateOfBirth,
    Guid ClassroomId,
    string ClassroomName,
    StudentStatus Status,
    bool HasAccount,
    byte[] RowVersion);

/// <summary>Who may read which students: admin and principal everything, a teacher only the classrooms they are homeroom teacher of.</summary>
internal static class StudentAccess
{
    public static bool CanRead(UserRole role) => role is UserRole.Admin or UserRole.Principal or UserRole.Teacher;

    public static IQueryable<Student> Scope(IQueryable<Student> students, ICurrentUser user) =>
        user.Role == UserRole.Teacher
            ? students.Where(s => s.Classroom!.HomeroomTeacher!.UserId == user.UserId)
            : students;
}
