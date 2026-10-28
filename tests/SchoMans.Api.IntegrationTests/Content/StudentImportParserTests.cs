using ClosedXML.Excel;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Infrastructure.Excel;

namespace SchoMans.Api.IntegrationTests.Content;

public class StudentImportParserTests
{
    private readonly StudentImportParser _parser = new();

    private static MemoryStream Stream(byte[] workbook) => new(workbook);

    private static string?[] Row(string nisn, string name, string email, string born, string classroom) => [nisn, name, email, born, classroom];

    [Fact]
    public void Rows_are_read_with_their_row_numbers_and_trimmed_values()
    {
        var workbook = TestData.Workbook(
            Row(" 1000000001 ", " Rina Putri ", " rina@schomans.demo ", "2013-05-17", " Class A "),
            Row("1000000002", "Agus Putra", "agus@schomans.demo", "18/05/2013", "Class B"));

        var result = _parser.Parse(Stream(workbook));

        Assert.Null(result.FileError);
        Assert.Equal([2, 3], result.Rows.Select(r => r.RowNumber));
        var first = result.Rows[0];
        Assert.Equal("1000000001", first.Nisn);
        Assert.Equal("Rina Putri", first.FullName);
        Assert.Equal("rina@schomans.demo", first.Email);
        Assert.Equal(new DateOnly(2013, 5, 17), first.DateOfBirth);
        Assert.Equal("Class A", first.Classroom);
        Assert.Equal(new DateOnly(2013, 5, 18), result.Rows[1].DateOfBirth);
    }

    [Fact]
    public void An_nisn_with_leading_zeros_keeps_them_when_the_column_is_text()
    {
        var result = _parser.Parse(Stream(TestData.Workbook(Row("0012345678", "A", "a@b.test", "2013-05-17", "Class A"))));

        Assert.Equal("0012345678", result.Rows.Single().Nisn);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("2013-13-45")]
    [InlineData("31/02/2013")]
    [InlineData("")]
    public void A_date_that_cannot_be_read_is_reported_as_missing_but_the_text_is_kept_for_the_message(string text)
    {
        var result = _parser.Parse(Stream(TestData.Workbook(Row("1000000001", "A", "a@b.test", text, "Class A"))));

        Assert.Null(result.Rows.Single().DateOfBirth);
        Assert.Equal(text, result.Rows.Single().DateOfBirthText);
    }

    [Fact]
    public void Blank_lines_are_skipped_but_keep_the_numbering_of_the_rows_after_them()
    {
        var workbook = TestData.Workbook(
            Row("1000000001", "A", "a@b.test", "2013-05-17", "Class A"),
            [null, null, null, null, null],
            Row("1000000002", "B", "b@b.test", "2013-05-17", "Class A"));

        var result = _parser.Parse(Stream(workbook));

        Assert.Equal([2, 4], result.Rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Headers_are_matched_ignoring_case_spaces_and_order_and_extra_columns_are_ignored()
    {
        var workbook = TestData.WorkbookWithHeader(
            ["Notes", "classroom", "Date Of Birth", "E-mail ignored", "EMAIL", "full name", "NISN"],
            ["ignore me", "Class A", "2013-05-17", "x", "a@b.test", "Rina", "1000000001"]);

        var result = _parser.Parse(Stream(workbook));

        Assert.Null(result.FileError);
        var row = result.Rows.Single();
        Assert.Equal("1000000001", row.Nisn);
        Assert.Equal("Rina", row.FullName);
        Assert.Equal("a@b.test", row.Email);
        Assert.Equal("Class A", row.Classroom);
        Assert.Equal(new DateOnly(2013, 5, 17), row.DateOfBirth);
    }

    [Fact]
    public void Missing_columns_are_named_in_the_error()
    {
        var result = _parser.Parse(Stream(TestData.WorkbookWithHeader(["Nisn", "FullName"], ["1", "x"])));

        Assert.NotNull(result.FileError);
        Assert.Contains("Email", result.FileError, StringComparison.Ordinal);
        Assert.Contains("DateOfBirth", result.FileError, StringComparison.Ordinal);
        Assert.Contains("Classroom", result.FileError, StringComparison.Ordinal);
        Assert.DoesNotContain("Nisn,", result.FileError, StringComparison.Ordinal);
        Assert.Empty(result.Rows);
    }

    [Theory]
    [InlineData("this is plain text")]
    [InlineData("")]
    public void A_file_that_is_not_a_workbook_gives_a_clear_error(string content)
    {
        var result = _parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));

        Assert.Contains("not a valid .xlsx", result.FileError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_zip_that_is_not_a_workbook_gives_the_same_clear_error()
    {
        using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            writer.Write("not a workbook");
        }

        zip.Position = 0;

        Assert.Contains("not a valid .xlsx", _parser.Parse(zip).FileError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_workbook_with_only_a_header_has_no_rows_and_no_error()
    {
        var result = _parser.Parse(Stream(TestData.Workbook()));

        Assert.Null(result.FileError);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void The_sheet_named_students_is_used_even_when_it_is_not_the_first()
    {
        using var book = new XLWorkbook();
        book.AddWorksheet("Cover").Cell(1, 1).Value = "Nothing to import here";
        var sheet = book.AddWorksheet("Students");
        string[] header = ["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"];
        for (var c = 0; c < header.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = header[c];
        }

        sheet.Cell(2, 1).Value = "1000000001";
        sheet.Cell(2, 2).Value = "Rina";
        sheet.Cell(2, 3).Value = "r@b.test";
        sheet.Cell(2, 4).Value = "2013-05-17";
        sheet.Cell(2, 5).Value = "Class A";
        using var stream = new MemoryStream();
        book.SaveAs(stream);

        var result = _parser.Parse(Stream(stream.ToArray()));

        Assert.Equal("Rina", result.Rows.Single().FullName);
    }

    [Theory]
    [InlineData(2000, false)]
    [InlineData(2001, true)]
    public void At_most_2000_rows_are_accepted(int rows, bool tooMany)
    {
        var data = Enumerable.Range(0, rows).Select(i => Row($"{i:0000000000}", "N", "n@b.test", "2013-05-17", "Class A")).ToArray();

        var result = _parser.Parse(Stream(TestData.Workbook(data)));

        Assert.Equal(tooMany, result.FileError is not null);
        Assert.Equal(tooMany ? 0 : rows, result.Rows.Count);
    }
}

public class StudentTemplateWriterTests
{
    [Fact]
    public void The_template_has_the_import_columns_text_formats_and_an_instructions_sheet()
    {
        var bytes = new StudentTemplateWriter().CreateTemplate();

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var sheet = book.Worksheet("Students");
        Assert.Equal(["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"], Enumerable.Range(1, 5).Select(c => sheet.Cell(1, c).GetString()));
        Assert.Equal("@", sheet.Column(1).Style.NumberFormat.Format);
        Assert.NotNull(book.Worksheet("Instructions"));
        Assert.Contains(book.Worksheet("Instructions").CellsUsed(), c => c.GetString().Contains("10 digits", StringComparison.Ordinal));
    }

    [Fact]
    public void A_filled_in_template_is_read_back_by_the_parser()
    {
        using var book = new XLWorkbook(new MemoryStream(new StudentTemplateWriter().CreateTemplate()));
        var sheet = book.Worksheet("Students");
        sheet.Cell(2, 1).Value = "0000000007";
        sheet.Cell(2, 2).Value = "Round Trip";
        sheet.Cell(2, 3).Value = "round@trip.test";
        sheet.Cell(2, 4).Value = new DateTime(2013, 5, 17);
        sheet.Cell(2, 5).Value = "Class A";
        using var stream = new MemoryStream();
        book.SaveAs(stream);

        var result = new StudentImportParser().Parse(new MemoryStream(stream.ToArray()));

        Assert.Null(result.FileError);
        var row = result.Rows.Single();
        Assert.Equal("0000000007", row.Nisn);
        Assert.Equal(new DateOnly(2013, 5, 17), row.DateOfBirth);
    }
}
