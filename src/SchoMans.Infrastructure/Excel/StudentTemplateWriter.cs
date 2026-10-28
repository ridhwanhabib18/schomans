using ClosedXML.Excel;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Excel;

/// <summary>The downloadable template of the student import: the header row with the right cell formats, and an instructions sheet.</summary>
public sealed class StudentTemplateWriter : IStudentTemplateWriter
{
    public byte[] CreateTemplate()
    {
        using var book = new XLWorkbook();

        var sheet = book.AddWorksheet("Students");
        for (var index = 0; index < StudentImportParser.Columns.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = StudentImportParser.Columns[index];
        }

        // Text format keeps leading zeros of an NISN; the date column shows dates the way the import reads them.
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(4).Style.NumberFormat.Format = "yyyy-mm-dd";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Columns(1, 5).Width = 22;

        var help = book.AddWorksheet("Instructions");
        string[] lines =
        [
            "Fill in one student per row on the 'Students' sheet, below the header row. Do not rename or remove the columns.",
            string.Empty,
            "Nisn: exactly 10 digits, unique. Keep the cell as text so leading zeros survive.",
            "FullName: the student's full name.",
            "Email: unique; the student signs in with it.",
            "DateOfBirth: a date, written as yyyy-MM-dd (for example 2013-05-17) or dd/MM/yyyy.",
            "Classroom: the name of a classroom of the current academic year, exactly as shown on the Classrooms page.",
            string.Empty,
            "Rows with a problem are listed with the reason after the import. The other rows are still imported.",
            "Up to 2,000 rows per file.",
        ];
        for (var index = 0; index < lines.Length; index++)
        {
            help.Cell(index + 1, 1).Value = lines[index];
        }

        help.Column(1).Width = 110;

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
