using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Features.ProgressSummaries;

public sealed record GradeFact(string Subject, string Component, decimal Score);

/// <summary>What the school knows about one student in one month: the only input of a progress summary.</summary>
internal sealed record ProgressFacts(
    string Month,
    int Present,
    int Excused,
    int Sick,
    int Absent,
    IReadOnlyList<GradeFact> Grades,
    int Submitted,
    int SubmittedLate,
    int NotHandedIn,
    decimal? AverageSubmissionScore,
    IReadOnlyList<string> Notes)
{
    public bool HasData =>
        Present + Excused + Sick + Absent > 0 || Grades.Count > 0 || Submitted > 0 || NotHandedIn > 0 || Notes.Count > 0;
}

internal static class ProgressFactsReader
{
    private const int MaxGrades = 40;
    private const int MaxNotes = 20;

    /// <summary>The first day of the month and of the next one, for <c>yyyy-MM</c>.</summary>
    public static (DateOnly First, DateOnly Next) MonthRange(string month)
    {
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return (first, first.AddMonths(1));
    }

    public static async Task<ProgressFacts> ReadAsync(
        IAppDbContext db, ISchoolClock clock, Guid studentId, Guid classroomId, string month, CancellationToken ct)
    {
        var (first, next) = MonthRange(month);
        var from = clock.ToUtc(first, TimeOnly.MinValue);
        var until = clock.ToUtc(next, TimeOnly.MinValue);

        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Session!.Date >= first && r.Session.Date < next)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var grades = await db.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.UpdatedAt >= from && g.UpdatedAt < until)
            .OrderBy(g => g.Subject!.Name).ThenBy(g => g.Component)
            .Select(g => new { Subject = g.Subject!.Name, g.Component, g.Score })
            .Take(MaxGrades)
            .ToListAsync(ct);

        var submissions = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.SubmittedAt >= from && s.SubmittedAt < until)
            .Select(s => new { s.IsLate, s.Score })
            .ToListAsync(ct);

        var notHandedIn = await db.Assignments.AsNoTracking().CountAsync(
            a => a.DueAt >= from && a.DueAt < until
                && a.Classrooms.Any(c => c.ClassroomId == classroomId)
                && !db.Submissions.Any(s => s.AssignmentId == a.Id && s.StudentId == studentId),
            ct);

        var notes = await db.TeacherNotes.AsNoTracking()
            .Where(n => n.StudentId == studentId && n.Date >= first && n.Date < next)
            .OrderBy(n => n.Date).ThenBy(n => n.CreatedAt)
            .Select(n => n.Body)
            .Take(MaxNotes)
            .ToListAsync(ct);

        int Count(AttendanceStatus status) => attendance.FirstOrDefault(a => a.Status == status)?.Count ?? 0;
        var scores = submissions.Where(s => s.Score is not null).Select(s => s.Score!.Value).ToList();

        return new ProgressFacts(
            month,
            Count(AttendanceStatus.Present),
            Count(AttendanceStatus.Excused),
            Count(AttendanceStatus.Sick),
            Count(AttendanceStatus.Absent),
            grades.Select(g => new GradeFact(g.Subject, g.Component.ToString(), g.Score)).ToList(),
            submissions.Count,
            submissions.Count(s => s.IsLate),
            notHandedIn,
            scores.Count == 0 ? null : Math.Round(scores.Average(), 1),
            notes);
    }
}
