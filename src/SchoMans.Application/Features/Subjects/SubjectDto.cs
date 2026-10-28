using System.Linq.Expressions;
using SchoMans.Application.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Subjects;

public sealed record SubjectDto(Guid Id, string Name, decimal MinimumPassingScore, string RowVersion);

internal static class SubjectMapping
{
    public static readonly Expression<Func<Subject, SubjectRow>> Projection = s => new SubjectRow(s.Id, s.Name, s.MinimumPassingScore, s.RowVersion);

    public static SubjectDto ToDto(SubjectRow row) => new(row.Id, row.Name, row.MinimumPassingScore, RowVersionCodec.Encode(row.RowVersion));

    public static bool CanRead(UserRole role) => role is UserRole.Admin or UserRole.Principal or UserRole.Teacher;
}

internal sealed record SubjectRow(Guid Id, string Name, decimal MinimumPassingScore, byte[] RowVersion);
