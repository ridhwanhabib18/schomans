using ClosedXML.Excel;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance;

namespace SchoMans.Infrastructure.Excel;

/// <summary>One sheet per export: a title row, a header, one row per student with counts and the present percentage.</summary>
public sealed class AttendanceExcelWriter : IAttendanceExport
{
    private static readonly string[] Headers = ["NISN", "Name", "Present", "Excused", "Sick", "Absent", "Lessons", "Present %"];

    public byte[] Write(ClassroomAttendanceSummaryDto summary)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Attendance");

        sheet.Cell(1, 1).Value = $"Attendance {summary.ClassroomName} {summary.Month}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Lessons with attendance: {summary.SessionCount}";

        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(4, c + 1).Value = Headers[c];
        }

        sheet.Row(4).Style.Font.Bold = true;

        var row = 5;
        foreach (var student in summary.Students)
        {
            // NISNs keep their leading zeros: write them as text.
            sheet.Cell(row, 1).SetValue(student.Nisn);
            sheet.Cell(row, 1).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 2).Value = student.FullName;
            sheet.Cell(row, 3).Value = student.Counts.Present;
            sheet.Cell(row, 4).Value = student.Counts.Excused;
            sheet.Cell(row, 5).Value = student.Counts.Sick;
            sheet.Cell(row, 6).Value = student.Counts.Absent;
            sheet.Cell(row, 7).Value = student.Counts.Total;
            sheet.Cell(row, 8).Value = student.Counts.PresentPercent;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
