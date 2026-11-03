using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.LearningObjectives.DeleteLearningObjective;

public sealed record DeleteLearningObjectiveCommand(Guid Id);

/// <summary>A learning objective that scores are linked to stays: removing it would change what those scores mean.</summary>
public sealed class DeleteLearningObjectiveHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Unit>> Handle(DeleteLearningObjectiveCommand command, CancellationToken ct)
    {
        var objective = await db.LearningObjectives.FirstOrDefaultAsync(o => o.Id == command.Id, ct);
        if (objective is null)
        {
            return Error.NotFound("objectives.not_found", "The learning objective does not exist.");
        }

        if (!await LearningObjectiveMapping.TeachesSubjectAsync(db, currentUser, objective.SubjectId, ct))
        {
            return Error.Forbidden("objectives.not_your_subject", "You only manage the learning objectives of the subjects you teach.");
        }

        if (await db.Grades.AnyAsync(g => g.LearningObjectiveId == objective.Id, ct))
        {
            return Error.Conflict("objectives.in_use", "Scores are linked to this learning objective, so it cannot be removed.");
        }

        db.LearningObjectives.Remove(objective);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
