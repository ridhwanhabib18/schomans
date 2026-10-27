using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Teachers;

public sealed record TeacherDto(Guid Id, string Nip, string FullName, string Email, bool HasAccount, bool IsHomeroomTeacher, string RowVersion);

internal sealed record TeacherRow(Guid Id, string Nip, string FullName, string Email, bool HasAccount, byte[] RowVersion);

internal static class TeacherMapping
{
    public static readonly Expression<Func<Teacher, TeacherRow>> Projection = t => new TeacherRow(
        t.Id,
        t.Nip,
        t.User!.FullName,
        t.User.Email,
        t.User.KeycloakId != null,
        t.RowVersion);

    /// <summary>Which of these teachers are the homeroom teacher of at least one classroom (one query for the whole page).</summary>
    public static async Task<HashSet<Guid>> HomeroomTeacherIds(IAppDbContext db, IReadOnlyCollection<Guid> teacherIds, CancellationToken ct) =>
        (await db.Classrooms.AsNoTracking()
            .Where(c => teacherIds.Contains(c.HomeroomTeacherId))
            .Select(c => c.HomeroomTeacherId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

    public static TeacherDto ToDto(TeacherRow row, bool isHomeroomTeacher) => new(
        row.Id, row.Nip, row.FullName, row.Email, row.HasAccount, isHomeroomTeacher, RowVersionCodec.Encode(row.RowVersion));
}
