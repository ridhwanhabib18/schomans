using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.LearningObjectives.CreateLearningObjective;

/// <summary>A teacher of the subject adds a learning objective. The code is used once per subject.</summary>
public sealed class CreateLearningObjectiveHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<LearningObjectiveDto>> Handle(CreateLearningObjectiveCommand command, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == command.SubjectId, ct))
        {
            return Error.NotFound("objectives.subject_not_found", "The subject does not exist.");
        }

        if (!await LearningObjectiveMapping.TeachesSubjectAsync(db, currentUser, command.SubjectId, ct))
        {
            return Error.Forbidden("objectives.not_your_subject", "You only manage the learning objectives of the subjects you teach.");
        }

        var code = command.Code.Trim();
        if (await db.LearningObjectives.AnyAsync(o => o.SubjectId == command.SubjectId && o.Code == code, ct))
        {
            return Error.Conflict("objectives.code_taken", "This subject already has a learning objective with that code.");
        }

        var objective = LearningObjective.Create(command.SubjectId, command.Code, command.Description);
        db.LearningObjectives.Add(objective);
        await db.SaveChangesAsync(ct);
        return objective.ToDto();
    }
}
