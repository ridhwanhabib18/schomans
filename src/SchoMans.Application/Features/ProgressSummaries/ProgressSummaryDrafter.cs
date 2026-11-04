using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.ProgressSummaries;

/// <summary>
/// Drafts the summary of one student for one month: reads the facts, asks the AI through the gateway (names replaced by a
/// code), checks the answer, and stores it as a draft. A student with no data that month gets no summary: there is nothing to say.
/// </summary>
public sealed class ProgressSummaryDrafter(IAppDbContext db, ISchoolClock clock, AiGateway gateway)
{
    public const string NoDataCode = "progress.no_data";

    /// <param name="userId">The teacher who asked, or null for the monthly job.</param>
    public async Task<Result<ProgressSummary>> DraftAsync(Guid? userId, Guid studentId, string month, CancellationToken ct)
    {
        var student = await db.Students
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.ClassroomId, s.Status, Name = s.User!.FullName })
            .FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return Error.NotFound("progress.student_not_found", "The student does not exist.");
        }

        var existing = await db.ProgressSummaries.FirstOrDefaultAsync(s => s.StudentId == studentId && s.Month == month, ct);
        if (existing?.Status == ProgressSummaryStatus.Sent)
        {
            return Error.Conflict("progress.already_sent", "The summary of this month was already sent to the parents.");
        }

        var facts = await ProgressFactsReader.ReadAsync(db, clock, studentId, student.ClassroomId, month, ct);
        if (!facts.HasData)
        {
            return Error.Conflict(NoDataCode, "There is no data of this student in that month to write a summary from.");
        }

        var aliases = new NameAliases();
        var alias = aliases.For(student.Id, student.Name);
        var completion = await gateway.RunAsync(userId, ProgressSummaryPrompt.Build(facts, alias, aliases), ct);
        if (!completion.IsSuccess)
        {
            return completion.Error;
        }

        var body = ProgressSummaryPrompt.ParseBody(completion.Value.Text, aliases);
        if (!body.IsSuccess)
        {
            return body.Error;
        }

        if (existing is null)
        {
            existing = ProgressSummary.Draft(studentId, month, body.Value, isAiDrafted: true);
            db.ProgressSummaries.Add(existing);
        }
        else
        {
            existing.Redraft(body.Value);
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
