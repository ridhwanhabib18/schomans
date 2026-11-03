using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;

namespace SchoMans.Application.Features.AchievementDescriptions;

public sealed record ObjectiveFact(string Code, string Description, decimal? Score);

public sealed record ComponentFact(string Component, decimal Average);

/// <summary>What the school knows about one student in one subject and term: the only input of an achievement description.</summary>
internal sealed record AchievementFacts(
    string Subject,
    string Term,
    decimal MinimumPassingScore,
    decimal? FinalGrade,
    string? Band,
    IReadOnlyList<ComponentFact> Components,
    IReadOnlyList<ObjectiveFact> Objectives,
    IReadOnlyList<string> Notes)
{
    /// <summary>A description is written from scores; without any there is nothing to say about the subject.</summary>
    public bool HasData => FinalGrade is not null;
}

internal static class AchievementFactsReader
{
    private const int MaxNotes = 10;

    public static async Task<AchievementFacts> ReadAsync(
        IAppDbContext db, Guid studentId, Guid subjectId, string subjectName, Guid termId, string termName, DateOnly termStart, DateOnly termEnd, CancellationToken ct)
    {
        var result = (await Transcript.BuildAsync(db, studentId, termId, ct)).FirstOrDefault(r => r.SubjectId == subjectId);

        var scores = await db.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.SubjectId == subjectId && g.TermId == termId && g.LearningObjectiveId != null)
            .Select(g => new { g.LearningObjectiveId, g.Score })
            .ToListAsync(ct);

        var objectives = (await db.LearningObjectives.AsNoTracking()
                .Where(o => o.SubjectId == subjectId)
                .OrderBy(o => o.Code)
                .Select(o => new { o.Id, o.Code, o.Description })
                .ToListAsync(ct))
            .Select(o =>
            {
                var own = scores.Where(s => s.LearningObjectiveId == o.Id).Select(s => s.Score).ToList();
                return new ObjectiveFact(o.Code, o.Description, own.Count == 0 ? null : decimal.Round(own.Average(), 1));
            })
            .ToList();

        var notes = await db.TeacherNotes.AsNoTracking()
            .Where(n => n.StudentId == studentId && n.Date >= termStart && n.Date <= termEnd)
            .OrderByDescending(n => n.Date)
            .Select(n => n.Body)
            .Take(MaxNotes)
            .ToListAsync(ct);

        return new AchievementFacts(
            subjectName,
            termName,
            result?.MinimumPassingScore ?? 0,
            result?.FinalGrade,
            result?.Band,
            result?.Components.Where(c => c.Average is not null).Select(c => new ComponentFact(c.Component.ToString(), c.Average!.Value)).ToList() ?? [],
            objectives,
            notes);
    }
}
