namespace SchoMans.Application.Features.Students.ImportStudents;

/// <param name="File">The uploaded .xlsx workbook.</param>
/// <param name="FileName">The name the browser sent, used to check the extension.</param>
/// <param name="Length">The size in bytes, checked before the file is read.</param>
public sealed record ImportStudentsCommand(Stream File, string FileName, long Length)
{
    public const long MaxBytes = 5 * 1024 * 1024;
}

public sealed record ImportRowError(int Row, string Message);

/// <summary>Rows that fail are listed with the reason; the other rows are still imported.</summary>
public sealed record ImportStudentsResult(int TotalRows, int Created, IReadOnlyList<ImportRowError> Failed);
