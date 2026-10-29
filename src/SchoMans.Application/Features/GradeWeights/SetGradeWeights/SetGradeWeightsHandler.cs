using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeWeights.SetGradeWeights;

/// <summary>A teacher who teaches the subject sets how its components count in the final grade.</summary>
public sealed class SetGradeWeightsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<GradeWeightsDto>> Handle(SetGradeWeightsCommand command, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == command.SubjectId, ct))
        {
            return Error.NotFound("subjects.not_found", "The subject does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null || !await db.Schedules.AnyAsync(s => s.TeacherId == teacherId && s.SubjectId == command.SubjectId, ct))
        {
            return Error.Forbidden("grades.not_your_subject", "Only a teacher of this subject can set its weights.");
        }

        var existing = await db.GradeWeights.Where(w => w.SubjectId == command.SubjectId).ToDictionaryAsync(w => w.Component, ct);
        foreach (var item in command.Weights)
        {
            if (existing.TryGetValue(item.Component, out var weight))
            {
                weight.Update(item.WeightPercent);
            }
            else
            {
                db.GradeWeights.Add(GradeWeight.Create(command.SubjectId, item.Component, item.WeightPercent));
            }
        }

        await db.SaveChangesAsync(ct);
        return await GradeWeightsLoader.LoadAsync(db, command.SubjectId, ct);
    }
}
