using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.Grading;
using SchoMans.Application.Features.ReportCards;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.AchievementDescriptions;

/// <summary>
/// Drafts the achievement description of one student for one subject and term: reads the facts, asks the AI through the gateway
/// (names replaced by a code), checks the answer, makes sure it is not word for word the text of a classmate, and stores it as a
/// draft that the teacher still has to accept. The caller has already checked that the user may write for this subject and classroom.
/// </summary>
public sealed class AchievementDescriptionDrafter(IAppDbContext db, AiGateway gateway)
{
    public const string NoDataCode = "achievement.no_data";

    private static string VaryInstruction(int variation) =>
        $"Note: your previous text was word for word the same as the text of another student. Use clearly different wording and sentence structure (variation {variation}).";

    /// <param name="userId">The teacher who asked.</param>
    /// <param name="bulk">A further call of a bulk run: it does not use up another of the teacher's daily calls.</param>
    public async Task<Result<AchievementDescriptionDto>> DraftAsync(Guid userId, Guid studentId, Guid subjectId, Guid termId, bool bulk, CancellationToken ct)
    {
        var student = await db.Students.Where(s => s.Id == studentId).Select(s => new { s.Id, s.ClassroomId, Name = s.User!.FullName }).FirstOrDefaultAsync(ct);
        var subject = await db.Subjects.Where(s => s.Id == subjectId).Select(s => new { s.Name }).FirstOrDefaultAsync(ct);
        var term = await db.Terms.Where(t => t.Id == termId).Select(t => new { t.Name, t.StartDate, t.EndDate }).FirstOrDefaultAsync(ct);
        if (student is null || subject is null || term is null)
        {
            return Error.NotFound("grades.not_found", "The student, subject or term does not exist.");
        }

        if (await GradeBook.IsPublishedAsync(db, student.ClassroomId, termId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a description.");
        }

        var facts = await AchievementFactsReader.ReadAsync(db, studentId, subjectId, subject.Name, termId, term.Name, term.StartDate, term.EndDate, ct);
        if (!facts.HasData)
        {
            return Error.Conflict(NoDataCode, "This student has no scores in the subject this term to write a description from.");
        }

        var classmates = await ClassmateFingerprintsAsync(student.ClassroomId, studentId, subjectId, termId, ct);

        string? body = null;
        for (var attempt = 0; attempt < 2 && body is null; attempt++)
        {
            var aliases = new NameAliases();
            var alias = aliases.For(student.Id, student.Name);
            var request = AchievementPrompt.Build(facts, alias, aliases, attempt == 0 ? null : VaryInstruction(classmates.Count + 1));

            var completion = await gateway.RunAsync(userId, request, ct, bulk: bulk || attempt > 0);
            if (!completion.IsSuccess)
            {
                return completion.Error;
            }

            var parsed = AchievementPrompt.ParseBody(completion.Value.Text, aliases);
            if (!parsed.IsSuccess)
            {
                return parsed.Error;
            }

            if (!classmates.Contains(AchievementPrompt.Fingerprint(parsed.Value, student.Name)))
            {
                body = parsed.Value;
            }
        }

        if (body is null)
        {
            return Error.Unavailable("achievement.duplicate", "The AI wrote the same text as for another student. Try again.");
        }

        var reportCard = await db.ReportCards.Include(r => r.Descriptions).FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId, ct);
        if (reportCard is null)
        {
            reportCard = ReportCard.Create(studentId, termId);
            db.ReportCards.Add(reportCard);
        }

        var description = reportCard.Descriptions.FirstOrDefault(d => d.SubjectId == subjectId);
        if (description is null)
        {
            description = AchievementDescription.Create(reportCard.Id, subjectId, body);
            reportCard.Descriptions.Add(description);
        }

        description.Redraft(body);
        await db.SaveChangesAsync(ct);
        return new AchievementDescriptionDto(studentId, subjectId, termId, description.Body, description.Status, description.IsAiDrafted);
    }

    /// <summary>The texts the classmates already have for this subject and term, without their names.</summary>
    private async Task<HashSet<string>> ClassmateFingerprintsAsync(Guid classroomId, Guid studentId, Guid subjectId, Guid termId, CancellationToken ct)
    {
        var others = await db.ReportCards.AsNoTracking()
            .Where(r => r.TermId == termId && r.StudentId != studentId && r.Student!.ClassroomId == classroomId)
            .SelectMany(r => r.Descriptions.Where(d => d.SubjectId == subjectId).Select(d => new { Name = r.Student!.User!.FullName, d.Body }))
            .ToListAsync(ct);

        return others.Select(o => AchievementPrompt.Fingerprint(o.Body, o.Name)).ToHashSet();
    }
}
