using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.ReportCards.GenerateReportCards;

public sealed record GenerateReportCardsCommand(Guid ClassroomId, Guid TermId);

public sealed record GeneratedReportCardsDto(Guid ClassroomId, Guid TermId, int Count, DateTimeOffset GeneratedAt);

/// <summary>
/// The homeroom teacher turns the published grades of the classroom into one PDF report card per student. Running it
/// again replaces the PDFs, for example after a correction and a new publication.
/// </summary>
public sealed class GenerateReportCardsHandler(IAppDbContext db, ICurrentUser currentUser, IReportCardPdf pdf, IFileStorage storage, ISchoolClock clock)
{
    public async Task<Result<GeneratedReportCardsDto>> Handle(GenerateReportCardsCommand command, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().Where(c => c.Id == command.ClassroomId)
            .Select(c => new { c.Name, c.HomeroomTeacherId, TeacherName = c.HomeroomTeacher!.User!.FullName, Year = c.AcademicYear!.Name })
            .FirstOrDefaultAsync(ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == command.TermId)
            .Select(t => new { t.Name, t.StartDate, t.EndDate }).FirstOrDefaultAsync(ct);
        if (classroom is null || term is null)
        {
            return Error.NotFound("grades.not_found", "The classroom or term does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != classroom.HomeroomTeacherId)
        {
            return Error.Forbidden("grades.not_homeroom", "Only the homeroom teacher generates the report cards of the classroom.");
        }

        if (!await GradeBook.IsPublishedAsync(db, command.ClassroomId, command.TermId, ct))
        {
            return Error.Conflict("report_cards.grades_not_published", "Publish the grades of this term before generating report cards.");
        }

        var students = await db.Students.AsNoTracking()
            .Where(s => s.ClassroomId == command.ClassroomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.User!.FullName)
            .Select(s => new { s.Id, s.User!.FullName, s.Nisn })
            .ToListAsync(ct);

        var now = clock.UtcNow;
        foreach (var student in students)
        {
            var results = await Transcript.BuildAsync(db, student.Id, command.TermId, ct);
            var reportCard = await db.ReportCards.Include(r => r.Descriptions)
                .FirstOrDefaultAsync(r => r.StudentId == student.Id && r.TermId == command.TermId, ct);
            if (reportCard is null)
            {
                reportCard = ReportCard.Create(student.Id, command.TermId);
                db.ReportCards.Add(reportCard);
            }

            var descriptions = reportCard.Descriptions.Where(d => d.Status == AchievementStatus.Accepted).ToDictionary(d => d.SubjectId, d => d.Body);
            var lines = results.Select(r => new ReportCardLine(
                r.SubjectName, r.MinimumPassingScore, r.FinalGrade, r.Band, r.IsPassing, descriptions.GetValueOrDefault(r.SubjectId))).ToList();

            var document = new ReportCardDocument(
                student.FullName,
                student.Nisn,
                classroom.Name,
                term.Name,
                classroom.Year,
                classroom.TeacherName,
                clock.Today,
                await AttendanceOfAsync(student.Id, term.StartDate, term.EndDate, ct),
                lines);

            var key = ReportCardAccess.PdfKey(command.TermId, student.Id);
            await storage.UploadAsync(FileBucket.Private, key, pdf.Render(document), "application/pdf", ct);

            reportCard.AttachPdf(key, now);
            reportCard.Publish();
        }

        await db.SaveChangesAsync(ct);
        return new GeneratedReportCardsDto(command.ClassroomId, command.TermId, students.Count, now);
    }

    private async Task<AttendanceSummary> AttendanceOfAsync(Guid studentId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var counts = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Session!.Date >= from && r.Session.Date <= to)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Of(AttendanceStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
        return new AttendanceSummary(Of(AttendanceStatus.Present), Of(AttendanceStatus.Excused), Of(AttendanceStatus.Sick), Of(AttendanceStatus.Absent));
    }
}
