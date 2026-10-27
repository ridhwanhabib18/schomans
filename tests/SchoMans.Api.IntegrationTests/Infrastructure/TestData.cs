using System.Net.Http.Headers;
using ClosedXML.Excel;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Students.CreateStudent;
using SchoMans.Application.Features.Teachers;
using SchoMans.Application.Features.Teachers.CreateTeacher;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>Builders for the data the tests create. Everything is unique per call, so tests never collide on the shared database.</summary>
public static class TestData
{
    private static long _nisn = 7_000_000_000;
    private static long _nip = 198_001_012_005_020_000;
    private static int _counter;

    public static string NextNisn() => Interlocked.Increment(ref _nisn).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string NextNip() => Interlocked.Increment(ref _nip).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string NextEmail(string prefix = "person") =>
        $"{prefix}-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@test.example";

    public static async Task<ClassroomDto> ClassroomAsync(HttpClient admin, string name = "Class A")
    {
        var page = await (await admin.GetAsync($"/api/v1/classrooms?search={Uri.EscapeDataString(name)}&pageSize=100")).ReadAsync<PagedResult<ClassroomDto>>();
        return page.Items.First(c => c.Name == name && c.AcademicYearName == "2026/2027");
    }

    public static async Task<StudentDto> CreateStudentAsync(HttpClient admin, Guid classroomId, string? email = null, string? nisn = null)
    {
        var response = await admin.PostJsonAsync("/api/v1/students", new CreateStudentCommand(
            "Test Student", email ?? NextEmail("student"), nisn ?? NextNisn(), classroomId, new DateOnly(2013, 5, 17)));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<StudentDto>();
    }

    public static async Task<TeacherDto> CreateTeacherAsync(HttpClient admin)
    {
        var response = await admin.PostJsonAsync("/api/v1/teachers", new CreateTeacherCommand("Test Teacher", NextEmail("teacher"), NextNip()));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<TeacherDto>();
    }

    /// <summary>An .xlsx workbook with the import header and the given rows (Nisn, FullName, Email, DateOfBirth, Classroom).</summary>
    public static byte[] Workbook(params string?[][] rows) => WorkbookWithHeader(["Nisn", "FullName", "Email", "DateOfBirth", "Classroom"], rows);

    public static byte[] WorkbookWithHeader(string[] header, params string?[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Students");
        sheet.Column(1).Style.NumberFormat.Format = "@";
        for (var column = 0; column < header.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = header[column];
        }

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] is { } text)
                {
                    sheet.Cell(row + 2, column + 1).Value = text;
                }
            }
        }

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    public static MultipartFormDataContent FileContent(byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>A tiny valid-looking PNG header; tests only need bytes, the storage fake never decodes them.</summary>
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
}
