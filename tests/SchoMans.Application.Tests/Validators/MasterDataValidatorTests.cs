using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.AcademicYears.CreateAcademicYear;
using SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;
using SchoMans.Application.Features.Classrooms.CreateClassroom;
using SchoMans.Application.Features.Classrooms.ListClassrooms;
using SchoMans.Application.Features.Students.CreateStudent;
using SchoMans.Application.Features.Students.ImportStudents;
using SchoMans.Application.Features.Students.ListStudents;
using SchoMans.Application.Features.Students.UpdateStudent;
using SchoMans.Application.Features.Subjects.CreateSubject;
using SchoMans.Application.Features.Teachers.CreateTeacher;
using SchoMans.Application.Features.Teachers.UpdateTeacher;

namespace SchoMans.Application.Tests.Validators;

public class MasterDataValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateOnly Start = new(2027, 7, 12);
    private static readonly DateOnly End = new(2028, 6, 17);
    private static readonly TermInput Term1 = new(null, "Term 1", Start, new DateOnly(2027, 12, 18));
    private static readonly TermInput Term2 = new(null, "Term 2", new DateOnly(2028, 1, 3), End);

    // ---- paging, shared by every list ----

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 100, true)]
    [InlineData(5, 1, true)]
    public void Lists_take_page_from_1_and_a_page_size_up_to_100(int page, int pageSize, bool valid)
    {
        Assert.Equal(valid, new ListStudentsValidator().Validate(new ListStudentsQuery(page, pageSize)).IsValid);
    }

    [Theory]
    [InlineData("name", true)]
    [InlineData("NISN", true)]
    [InlineData("dateOfBirth", true)]
    [InlineData(null, true)]
    [InlineData("password", false)]
    [InlineData("name; DROP TABLE Students", false)]
    public void Only_listed_fields_can_be_sorted_on(string? sort, bool valid)
    {
        Assert.Equal(valid, new ListStudentsValidator().Validate(new ListStudentsQuery(Sort: sort)).IsValid);
        Assert.Equal(valid, new ListClassroomsValidator().Validate(new ListClassroomsQuery(Sort: sort == "NISN" || sort == "dateOfBirth" ? "name" : sort)).IsValid);
    }

    // ---- students ----

    private static CreateStudentCommand Student(string? nisn = null, string? email = null, DateOnly? born = null, Guid? classroom = null) =>
        new("Rina Putri", email ?? "rina@schomans.demo", nisn ?? "1000000001", classroom ?? Id, born ?? new DateOnly(2013, 5, 17));

    [Fact]
    public void A_complete_student_is_valid()
    {
        Assert.True(new CreateStudentValidator().Validate(Student()).IsValid);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdefghij")]
    public void A_student_needs_a_ten_digit_nisn(string nisn)
    {
        Assert.False(new CreateStudentValidator().Validate(Student(nisn: nisn)).IsValid);
    }

    [Fact]
    public void A_student_needs_a_plausible_date_of_birth_in_the_past()
    {
        Assert.False(new CreateStudentValidator().Validate(Student(born: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))).IsValid);
        Assert.False(new CreateStudentValidator().Validate(Student(born: new DateOnly(1950, 1, 1))).IsValid);
    }

    [Fact]
    public void A_student_needs_a_classroom_and_an_email()
    {
        Assert.False(new CreateStudentValidator().Validate(Student(classroom: Guid.Empty)).IsValid);
        Assert.False(new CreateStudentValidator().Validate(Student(email: "nope")).IsValid);
    }

    [Fact]
    public void Updating_a_student_needs_the_row_version_from_the_read()
    {
        var update = new UpdateStudentCommand(Id, "Rina Putri", "rina@schomans.demo", "1000000001", Id, new DateOnly(2013, 5, 17), "AAAAAAAAB9E=");

        Assert.True(new UpdateStudentValidator().Validate(update).IsValid);
        Assert.False(new UpdateStudentValidator().Validate(update with { RowVersion = "" }).IsValid);
        Assert.False(new UpdateStudentValidator().Validate(update with { RowVersion = "%%%" }).IsValid);
    }

    [Fact]
    public void The_id_of_an_update_comes_from_the_route_so_the_body_does_not_need_it()
    {
        var update = new UpdateStudentCommand(Guid.Empty, "Rina Putri", "rina@schomans.demo", "1000000001", Id, new DateOnly(2013, 5, 17), "AAAAAAAAB9E=");

        Assert.True(new UpdateStudentValidator().Validate(update).IsValid);
    }

    // ---- the import file ----

    [Theory]
    [InlineData("students.xlsx", 1024, true)]
    [InlineData("STUDENTS.XLSX", 1024, true)]
    [InlineData("students.xls", 1024, false)]
    [InlineData("students.csv", 1024, false)]
    [InlineData("students.xlsx", 0, false)]
    [InlineData("students.xlsx", ImportStudentsCommand.MaxBytes + 1, false)]
    [InlineData("students.xlsx", ImportStudentsCommand.MaxBytes, true)]
    public void The_import_file_must_be_a_small_xlsx(string name, long length, bool valid)
    {
        var command = new ImportStudentsCommand(Stream.Null, name, length);

        Assert.Equal(valid, new ImportStudentsValidator().Validate(command).IsValid);
    }

    // ---- teachers ----

    [Fact]
    public void A_teacher_needs_an_eighteen_digit_nip()
    {
        Assert.True(new CreateTeacherValidator().Validate(new CreateTeacherCommand("Budi", "budi@schomans.demo", "198001012005011001")).IsValid);
        Assert.False(new CreateTeacherValidator().Validate(new CreateTeacherCommand("Budi", "budi@schomans.demo", "1980")).IsValid);
        Assert.False(new UpdateTeacherValidator().Validate(new UpdateTeacherCommand(Id, "Budi", "budi@schomans.demo", "1980", "AAAAAAAAB9E=")).IsValid);
    }

    // ---- classrooms and subjects ----

    [Fact]
    public void A_classroom_needs_a_name_a_year_and_a_homeroom_teacher()
    {
        var validator = new CreateClassroomValidator();

        Assert.True(validator.Validate(new CreateClassroomCommand("Class A", Id, Id)).IsValid);
        Assert.False(validator.Validate(new CreateClassroomCommand(" ", Id, Id)).IsValid);
        Assert.False(validator.Validate(new CreateClassroomCommand("Class A", Guid.Empty, Id)).IsValid);
        Assert.False(validator.Validate(new CreateClassroomCommand("Class A", Id, Guid.Empty)).IsValid);
        Assert.False(validator.Validate(new CreateClassroomCommand(new string('x', 51), Id, Id)).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(75.5, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void A_minimum_passing_score_is_between_0_and_100(double score, bool valid)
    {
        Assert.Equal(valid, new CreateSubjectValidator().Validate(new CreateSubjectCommand("Mathematics", (decimal)score)).IsValid);
    }

    // ---- academic years ----

    [Fact]
    public void A_valid_academic_year_has_one_to_four_terms_in_order()
    {
        var validator = new CreateAcademicYearValidator();

        Assert.True(validator.Validate(new CreateAcademicYearCommand("2027/2028", Start, End, [Term1, Term2])).IsValid);
        Assert.False(validator.Validate(new CreateAcademicYearCommand("2027/2028", Start, End, [])).IsValid);
        Assert.False(validator.Validate(new CreateAcademicYearCommand("2027/2028", End, Start, [Term1])).IsValid);
        Assert.False(validator.Validate(new CreateAcademicYearCommand("2027/2028", Start, End, [Term1 with { EndDate = Term1.StartDate }])).IsValid);
    }

    [Theory]
    [InlineData("2027-07-12", "2028-06-17", null)]
    public void The_year_shape_check_accepts_a_proper_year(string start, string end, string? expectedProblem)
    {
        Assert.Equal(expectedProblem, AcademicYearMapping.CheckYearShape(DateOnly.Parse(start), DateOnly.Parse(end), [Term1, Term2]));
    }

    [Fact]
    public void The_year_shape_check_finds_each_kind_of_problem()
    {
        Assert.NotNull(AcademicYearMapping.CheckYearShape(End, Start, [Term1]));
        Assert.NotNull(AcademicYearMapping.CheckYearShape(Start, End, []));
        Assert.NotNull(AcademicYearMapping.CheckYearShape(Start, End, [Term1, Term1 with { Id = Guid.NewGuid() }])); // same name twice
        Assert.NotNull(AcademicYearMapping.CheckYearShape(Start, End, [Term1 with { StartDate = Start.AddDays(-1) }])); // starts before the year
        Assert.NotNull(AcademicYearMapping.CheckYearShape(Start, End, [Term1 with { EndDate = End.AddDays(1) }]));    // ends after the year
        Assert.NotNull(AcademicYearMapping.CheckYearShape(Start, End, Enumerable.Range(1, 5).Select(i => Term1 with { Name = $"T{i}" }).ToList()));
    }

    // ---- rollover ----

    private static RolloverAcademicYearCommand Rollover(params RolloverMapping[] mappings) =>
        new("2027/2028", Start, End, [Term1, Term2], mappings);

    [Fact]
    public void A_rollover_maps_every_classroom_to_a_new_one_or_to_graduation()
    {
        var validator = new RolloverAcademicYearValidator();

        Assert.True(validator.Validate(Rollover(
            new RolloverMapping(Id, Graduate: false, "Class B", Id),
            new RolloverMapping(Guid.NewGuid(), Graduate: true, null, null))).IsValid);
    }

    [Fact]
    public void A_classroom_that_moves_on_needs_a_name_and_a_homeroom_teacher()
    {
        var validator = new RolloverAcademicYearValidator();

        Assert.False(validator.Validate(Rollover(new RolloverMapping(Id, Graduate: false, null, Id))).IsValid);
        Assert.False(validator.Validate(Rollover(new RolloverMapping(Id, Graduate: false, "Class B", null))).IsValid);
        Assert.False(validator.Validate(Rollover()).IsValid);
    }
}
