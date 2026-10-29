using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.GradeWeights.GetGradeWeights;

/// <summary>Admin and principal read any subject; a teacher only the subjects they teach.</summary>
public sealed class GetGradeWeightsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<GradeWeightsDto>> Handle(GetGradeWeightsQuery query, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == query.SubjectId, ct))
        {
            return Error.NotFound("subjects.not_found", "The subject does not exist.");
        }

        if (currentUser.Role == UserRole.Teacher && !await TeachesAsync(query.SubjectId, ct))
        {
            return Error.Forbidden("grades.not_your_subject", "You only see the weights of the subjects you teach.");
        }

        return await GradeWeightsLoader.LoadAsync(db, query.SubjectId, ct);
    }

    private async Task<bool> TeachesAsync(Guid subjectId, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        return teacherId is not null && await db.Schedules.AnyAsync(s => s.TeacherId == teacherId && s.SubjectId == subjectId, ct);
    }
}
