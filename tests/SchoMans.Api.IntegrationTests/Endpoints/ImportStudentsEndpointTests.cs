using System.Net;
using ClosedXML.Excel;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Students.ImportStudents;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class ImportStudentsEndpointTests(ApiFixture fixture)
{
    private const string ImportUrl = "/api/v1/students/import";

    private static string[] Row(string? nisn, string name, string? email, string? born, string? classroom) =>
        [nisn!, name, email!, born!, classroom!];

    private async Task<ImportStudentsSummary> ImportAsync(HttpClient admin, byte[] workbook, string fileName = "students.xlsx")
    {
        using var content = TestData.FileContent(workbook, fileName);
        var response = await admin.PostAsync(ImportUrl, content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<ImportStudentsSummary>();
    }

    [Fact]
    public async Task The_template_is_a_workbook_with_the_expected_columns_and_instructions()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.GetAsync("/api/v1/students/import-template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("students-import-template.xlsx", response.Content.Headers.ContentDisposition?.FileName);
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = book.Worksheet("Students");
        Assert.Equal(["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"], Enumerable.Range(1, 5).Select(c => sheet.Cell(1, c).GetString()));
        Assert.NotNull(book.Worksheet("Instructions"));
        Assert.Null(sheet.Cell(2, 1).Value.IsBlank ? null : "the template must not contain example students that could be imported by mistake");
    }

    [Fact]
    public async Task A_valid_file_creates_every_student()
    {
        using var admin = fixture.AsAdmin();
        var rows = Enumerable.Range(0, 3)
            .Select(i => Row(TestData.NextNisn(), $"Imported {i}", TestData.NextEmail("import"), "2013-05-17", "Class A"))
            .ToArray();

        var result = await ImportAsync(admin, TestData.Workbook(rows));

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(3, result.Created);
        Assert.Empty(result.Failed);
        foreach (var row in rows)
        {
            var found = await (await admin.GetAsync($"/api/v1/students?search={Uri.EscapeDataString(row[0])}")).ReadAsync<PagedResult<StudentDto>>();
            Assert.Single(found.Items);
            Assert.Equal("Class A", found.Items[0].ClassroomName);
            Assert.False(found.Items[0].HasAccount);
        }
    }

    [Fact]
    public async Task Rows_with_problems_are_listed_with_the_reason_and_the_good_rows_are_still_imported()
    {
        using var admin = fixture.AsAdmin();
        var goodNisn = TestData.NextNisn();
        var duplicateEmail = TestData.NextEmail("dup");
        var rows = new[]
        {
            Row(goodNisn, "Good Student", TestData.NextEmail("good"), "2013-05-17", "Class A"),                   // row 2: fine
            Row(TestData.NextNisn(), "Bad Date", TestData.NextEmail("x"), "not a date", "Class A"),                // row 3
            Row(TestData.NextNisn(), "No Classroom", TestData.NextEmail("x"), "2013-05-17", "Class Z"),            // row 4
            Row("123", "Short Nisn", TestData.NextEmail("x"), "2013-05-17", "Class A"),                             // row 5
            Row("1000000002", "Taken Nisn", TestData.NextEmail("x"), "2013-05-17", "Class A"),                      // row 6: a seeded student has it
            Row(TestData.NextNisn(), "First Dup", duplicateEmail, "2013-05-17", "Class A"),                        // row 7: fine
            Row(TestData.NextNisn(), "Second Dup", duplicateEmail, "2013-05-17", "Class A"),                       // row 8: email twice in the file
            Row(TestData.NextNisn(), "Taken Email", "teacher@schomans.demo", "2013-05-17", "Class A"),              // row 9: a seeded account has it
            Row(TestData.NextNisn(), "", "bad-email", "2013-05-17", "Class A"),                                     // row 10: no name, bad email
        };

        var result = await ImportAsync(admin, TestData.Workbook(rows));

        Assert.Equal(9, result.TotalRows);
        Assert.Equal(2, result.Created);
        Assert.Equal([3, 4, 5, 6, 8, 9, 10], result.Failed.Select(f => f.Row));
        string Reason(int row) => result.Failed.Single(f => f.Row == row).Message;
        Assert.Contains("valid date", Reason(3), StringComparison.Ordinal);
        Assert.Contains("'Class Z' does not exist", Reason(4), StringComparison.Ordinal);
        Assert.Contains("10 digits", Reason(5), StringComparison.Ordinal);
        Assert.Contains("already belongs to another student", Reason(6), StringComparison.Ordinal);
        Assert.Contains("appears twice", Reason(8), StringComparison.Ordinal);
        Assert.Contains("already belongs to another account", Reason(9), StringComparison.Ordinal);
        Assert.Contains("Full name", Reason(10), StringComparison.OrdinalIgnoreCase);

        var good = await (await admin.GetAsync($"/api/v1/students?search={Uri.EscapeDataString(goodNisn)}")).ReadAsync<PagedResult<StudentDto>>();
        Assert.Single(good.Items);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_does_not_create_duplicates()
    {
        using var admin = fixture.AsAdmin();
        var workbook = TestData.Workbook(Row(TestData.NextNisn(), "Once Only", TestData.NextEmail("once"), "2013-05-17", "Class A"));

        var first = await ImportAsync(admin, workbook);
        var second = await ImportAsync(admin, workbook);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Single(second.Failed);
    }

    [Fact]
    public async Task Blank_lines_between_rows_are_skipped_and_dd_mm_yyyy_dates_are_understood()
    {
        using var admin = fixture.AsAdmin();
        var rows = new[]
        {
            Row(TestData.NextNisn(), "First", TestData.NextEmail("a"), "17/05/2013", "class a"),
            Row(null, "", null, null, null),
            Row(TestData.NextNisn(), "Second", TestData.NextEmail("b"), "2013-05-18", "CLASS A"),
        };

        var result = await ImportAsync(admin, TestData.Workbook(rows));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Created);
    }

    [Fact]
    public async Task A_real_date_cell_is_accepted()
    {
        using var admin = fixture.AsAdmin();
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Students");
        string[] header = ["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"];
        for (var c = 0; c < header.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = header[c];
        }

        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Cell(2, 1).Value = TestData.NextNisn();
        sheet.Cell(2, 2).Value = "Date Cell";
        sheet.Cell(2, 3).Value = TestData.NextEmail("datecell");
        sheet.Cell(2, 4).Value = new DateTime(2013, 5, 17);
        sheet.Cell(2, 5).Value = "Class A";
        using var stream = new MemoryStream();
        book.SaveAs(stream);

        var result = await ImportAsync(admin, stream.ToArray());

        Assert.Equal(1, result.Created);
    }

    [Fact]
    public async Task A_file_without_the_required_columns_is_rejected_and_names_the_missing_ones()
    {
        using var admin = fixture.AsAdmin();
        var workbook = TestData.WorkbookWithHeader(["Nisn", "FullName"], ["1", "x"]);
        using var content = TestData.FileContent(workbook, "students.xlsx");

        var response = await admin.PostAsync(ImportUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("students.import_invalid_file", await response.ProblemCodeAsync());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email", body, StringComparison.Ordinal);
        Assert.Contains("Classroom", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Something_that_is_not_a_workbook_is_rejected_with_a_clear_message()
    {
        using var admin = fixture.AsAdmin();
        using var content = TestData.FileContent("this is not a zip file"u8.ToArray(), "students.xlsx");

        var response = await admin.PostAsync(ImportUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not a valid .xlsx", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("students.csv")]
    [InlineData("students.xls")]
    [InlineData("students")]
    public async Task Only_xlsx_files_are_accepted(string fileName)
    {
        using var admin = fixture.AsAdmin();
        using var content = TestData.FileContent(TestData.Workbook(), fileName);

        var response = await admin.PostAsync(ImportUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("students.import_invalid_file", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_sheet_with_too_many_rows_is_rejected_before_any_student_is_created()
    {
        using var admin = fixture.AsAdmin();
        var rows = Enumerable.Range(0, 2001).Select(i => Row($"{i:0000000000}", $"S{i}", $"s{i}@test.example", "2013-05-17", "Class A")).ToArray();
        using var content = TestData.FileContent(TestData.Workbook(rows), "students.xlsx");

        var response = await admin.PostAsync(ImportUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("2000", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    [InlineData("Student")]
    public async Task Only_admin_may_import_or_download_the_template(string role)
    {
        using var client = fixture.AsNewUser(role);
        using var content = TestData.FileContent(TestData.Workbook(), "students.xlsx");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(ImportUrl, content)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/students/import-template")).StatusCode);
    }

    [Fact]
    public async Task Importing_without_a_token_is_unauthorized()
    {
        using var client = fixture.Factory.CreateClient();
        using var content = TestData.FileContent(TestData.Workbook(), "students.xlsx");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(ImportUrl, content)).StatusCode);
    }
}
