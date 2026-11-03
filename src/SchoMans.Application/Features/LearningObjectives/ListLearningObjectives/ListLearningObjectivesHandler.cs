using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.LearningObjectives.ListLearningObjectives;

public sealed record ListLearningObjectivesQuery(Guid SubjectId);

/// <summary>The learning objectives of a subject, by code. Only a teacher of the subject sees them.</summary>
public sealed class ListLearningObjectivesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<List<LearningObjectiveDto>>> Handle(ListLearningObjectivesQuery query, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == query.SubjectId, ct))
        {
            return Error.NotFound("objectives.subject_not_found", "The subject does not exist.");
        }

        if (!await LearningObjectiveMapping.TeachesSubjectAsync(db, currentUser, query.SubjectId, ct))
        {
            return Error.Forbidden("objectives.not_your_subject", "You only see the learning objectives of the subjects you teach.");
        }

        return await db.LearningObjectives.AsNoTracking()
            .Where(o => o.SubjectId == query.SubjectId)
            .OrderBy(o => o.Code)
            .Select(o => new LearningObjectiveDto(o.Id, o.SubjectId, o.Code, o.Description))
            .ToListAsync(ct);
    }
}
