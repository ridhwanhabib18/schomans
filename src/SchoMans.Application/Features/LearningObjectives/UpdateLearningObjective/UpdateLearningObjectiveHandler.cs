using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.LearningObjectives.UpdateLearningObjective;

public sealed class UpdateLearningObjectiveHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<LearningObjectiveDto>> Handle(UpdateLearningObjectiveCommand command, CancellationToken ct)
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

        var code = command.Code.Trim();
        if (await db.LearningObjectives.AnyAsync(o => o.SubjectId == objective.SubjectId && o.Code == code && o.Id != objective.Id, ct))
        {
            return Error.Conflict("objectives.code_taken", "This subject already has a learning objective with that code.");
        }

        objective.Update(command.Code, command.Description);
        await db.SaveChangesAsync(ct);
        return objective.ToDto();
    }
}
