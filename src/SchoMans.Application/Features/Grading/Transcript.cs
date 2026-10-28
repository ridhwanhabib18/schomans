using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading;

internal sealed record SubjectResult(
    Guid SubjectId,
    string SubjectName,
    decimal MinimumPassingScore,
    List<ComponentScoreDto> Components,
    decimal? FinalGrade,
    string? Band,
    bool? IsPassing);

/// <summary>The results of one student in one term, per subject: used by the grades screen of students and parents and by the report card.</summary>
internal static class Transcript
{
    public static async Task<List<SubjectResult>> BuildAsync(IAppDbContext db, Guid studentId, Guid termId, CancellationToken ct)
    {
        var grades = await db.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.TermId == termId)
            .Select(g => new { g.SubjectId, g.Component, g.Score })
            .ToListAsync(ct);
        var subjectIds = grades.Select(g => g.SubjectId).Distinct().ToList();

        var subjects = await db.Subjects.AsNoTracking()
            .Where(s => subjectIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.MinimumPassingScore })
            .ToListAsync(ct);
        var storedWeights = (await db.GradeWeights.AsNoTracking()
                .Where(w => subjectIds.Contains(w.SubjectId))
                .Select(w => new { w.SubjectId, w.Component, w.WeightPercent })
                .ToListAsync(ct))
            .ToLookup(w => w.SubjectId);
        var bands = await db.GradeBands.AsNoTracking().ToListAsync(ct);

        return subjects.Select(subject =>
        {
            var own = grades.Where(g => g.SubjectId == subject.Id).ToList();
            var weights = storedWeights[subject.Id].Any()
                ? storedWeights[subject.Id].ToDictionary(w => w.Component, w => w.WeightPercent)
                : GradeCalculator.DefaultWeights.ToDictionary(w => w.Key, w => w.Value);

            var components = Enum.GetValues<GradeComponent>().Select(component =>
            {
                var ofComponent = own.Where(g => g.Component == component).ToList();
                return new ComponentScoreDto(component, ofComponent.Count == 0 ? null : decimal.Round(ofComponent.Average(g => g.Score), 2), ofComponent.Count);
            }).ToList();

            var final = GradeCalculator.FinalGrade(weights, own.Select(g => (g.Component, g.Score)));
            return new SubjectResult(
                subject.Id,
                subject.Name,
                subject.MinimumPassingScore,
                components,
                final,
                final is null ? null : GradeBand.LabelFor(final.Value, bands),
                final is null ? null : final >= subject.MinimumPassingScore);
        }).ToList();
    }
}
