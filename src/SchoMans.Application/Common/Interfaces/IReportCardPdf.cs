namespace SchoMans.Application.Common.Interfaces;

public sealed record AttendanceSummary(int Present, int Excused, int Sick, int Absent);

public sealed record ReportCardLine(string Subject, decimal MinimumPassingScore, decimal? FinalGrade, string? Band, bool? IsPassing, string? Description);

/// <summary>Everything printed on a term report card. The letterhead is the renderer's business, not the use case's.</summary>
public sealed record ReportCardDocument(
    string StudentName,
    string Nisn,
    string ClassroomName,
    string TermName,
    string AcademicYearName,
    string HomeroomTeacherName,
    DateOnly IssuedOn,
    AttendanceSummary Attendance,
    IReadOnlyList<ReportCardLine> Lines);

/// <summary>Turns a report card into a print-ready PDF (QuestPDF behind the interface).</summary>
public interface IReportCardPdf
{
    byte[] Render(ReportCardDocument document);
}
