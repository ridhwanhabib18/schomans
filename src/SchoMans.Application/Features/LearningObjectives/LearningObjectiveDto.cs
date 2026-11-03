using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.LearningObjectives;

public sealed record LearningObjectiveDto(Guid Id, Guid SubjectId, string Code, string Description);

internal static class LearningObjectiveMapping
{
    public static LearningObjectiveDto ToDto(this LearningObjective objective) =>
        new(objective.Id, objective.SubjectId, objective.Code, objective.Description);

    /// <summary>The learning objectives of a subject are kept by the teachers who teach it (in any classroom of the timetable).</summary>
    public static async Task<bool> TeachesSubjectAsync(IAppDbContext db, ICurrentUser currentUser, Guid subjectId, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        return teacherId is not null && await db.Schedules.AnyAsync(s => s.TeacherId == teacherId && s.SubjectId == subjectId, ct);
    }
}
