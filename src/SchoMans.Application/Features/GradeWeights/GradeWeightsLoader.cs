using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeWeights;

internal static class GradeWeightsLoader
{
    /// <summary>The weights of a subject, or the defaults while the subject has none of its own.</summary>
    public static async Task<GradeWeightsDto> LoadAsync(IAppDbContext db, Guid subjectId, CancellationToken ct)
    {
        var stored = await db.GradeWeights.AsNoTracking()
            .Where(w => w.SubjectId == subjectId)
            .ToDictionaryAsync(w => w.Component, w => w.WeightPercent, ct);

        var isDefault = stored.Count == 0;
        var items = Enum.GetValues<GradeComponent>()
            .Select(c => new GradeWeightItem(c, isDefault ? GradeCalculator.DefaultWeights[c] : stored.GetValueOrDefault(c)))
            .ToList();

        return new GradeWeightsDto(subjectId, items, isDefault);
    }
}
