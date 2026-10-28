namespace SchoMans.Application.Common.Interfaces;

/// <summary>One data row of the student import sheet, with the cells already read as text (the date also parsed).</summary>
public sealed record ParsedStudentRow(
    int RowNumber,
    string Nisn,
    string FullName,
    string Email,
    DateOnly? DateOfBirth,
    string DateOfBirthText,
    string Classroom);

/// <param name="FileError">Set when the file as a whole cannot be used (not a workbook, wrong columns, too many rows).</param>
public sealed record ParsedStudentImport(IReadOnlyList<ParsedStudentRow> Rows, string? FileError);

/// <summary>Reads the student import sheet (.xlsx). Implemented with ClosedXML in Infrastructure.</summary>
public interface IStudentImportParser
{
    ParsedStudentImport Parse(Stream workbook);
}

/// <summary>Builds the downloadable .xlsx template for the student import.</summary>
public interface IStudentTemplateWriter
{
    byte[] CreateTemplate();
}
