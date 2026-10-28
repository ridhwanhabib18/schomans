using System.Globalization;
using ClosedXML.Excel;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Excel;

/// <summary>Reads the student import sheet with ClosedXML. It only reads cells; every business rule is applied by the use case.</summary>
public sealed class StudentImportParser : IStudentImportParser
{
    public const int MaxRows = 2000;

    internal static readonly string[] Columns = ["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"];

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"];

    public ParsedStudentImport Parse(Stream workbook)
    {
        XLWorkbook book;
        try
        {
            book = new XLWorkbook(workbook);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Whatever is wrong with an uploaded file (not a zip, not a workbook, damaged), the user needs one clear message.
            return Failed("The file is not a valid .xlsx workbook. Download the template and fill it in.");
        }

        using (book)
        {
            var sheet = book.Worksheets.FirstOrDefault(w => w.Name.Equals("Students", StringComparison.OrdinalIgnoreCase))
                ?? book.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                return Failed("The workbook has no sheet.");
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxRows)
            {
                return Failed($"The sheet has more than {MaxRows} rows. Split it into several files.");
            }

            var columnByName = ReadHeader(sheet);
            var missing = Columns.Where(c => !columnByName.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                return Failed($"The header row is missing the column(s): {string.Join(", ", missing)}. Download the template to see the expected columns.");
            }

            var rows = new List<ParsedStudentRow>();
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var values = Columns.ToDictionary(c => c, c => Text(sheet.Cell(rowNumber, columnByName[c])));
                if (values.Values.All(v => v.Length == 0))
                {
                    continue; // a blank line between rows is not an error
                }

                rows.Add(new ParsedStudentRow(
                    rowNumber,
                    values["Nisn"],
                    values["FullName"],
                    values["Email"],
                    ReadDate(sheet.Cell(rowNumber, columnByName["DateOfBirth"])),
                    values["DateOfBirth"],
                    values["Classroom"]));
            }

            return new ParsedStudentImport(rows, null);
        }
    }

    private static ParsedStudentImport Failed(string message) => new([], message);

    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var name = Text(sheet.Cell(1, column)).Replace(" ", string.Empty, StringComparison.Ordinal);
            if (name.Length > 0)
            {
                columns.TryAdd(name, column);
            }
        }

        return columns;
    }

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();

    private static DateOnly? ReadDate(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date))
        {
            return DateOnly.FromDateTime(date);
        }

        return DateOnly.TryParseExact(Text(cell), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
