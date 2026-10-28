using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.ListTeachingOptions;

/// <summary>The classroom and subject pairs of the teacher's timetable in the current academic year: what the assignment form offers.</summary>
public sealed class ListTeachingOptionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<TeachingOptionDto>>> Handle(CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("assignments.not_a_teacher", "Only teachers give assignments.");
        }

        return await TeachingPairs(db, teacherId.Value)
            .OrderBy(p => p.SubjectName)
            .ThenBy(p => p.ClassroomName)
            .Select(p => new TeachingOptionDto(p.ClassroomId, p.ClassroomName, p.SubjectId, p.SubjectName))
            .ToListAsync(ct);
    }

    // A member-init type (not a record constructor) so EF can apply DISTINCT and ORDER BY in SQL.
    internal static IQueryable<TeachingPair> TeachingPairs(IAppDbContext db, Guid teacherId) =>
        db.Schedules.AsNoTracking()
            .Where(s => s.TeacherId == teacherId && s.Classroom!.AcademicYear!.IsActive)
            .Select(s => new TeachingPair
            {
                ClassroomId = s.ClassroomId,
                ClassroomName = s.Classroom!.Name,
                SubjectId = s.SubjectId,
                SubjectName = s.Subject!.Name,
            })
            .Distinct();

    internal sealed class TeachingPair
    {
        public Guid ClassroomId { get; init; }

        public string ClassroomName { get; init; } = string.Empty;

        public Guid SubjectId { get; init; }

        public string SubjectName { get; init; } = string.Empty;
    }
}
