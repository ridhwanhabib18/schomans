using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Assignments.CreateAssignment;
using SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;
using SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;
using SchoMans.Application.Features.Assignments.GradeSubmission;
using SchoMans.Application.Features.Assignments.UpdateAssignment;
using SchoMans.Application.Features.Assignments.SubmitAssignment;
using SchoMans.Application.Features.Attendance.GetClassroomSummary;
using SchoMans.Application.Features.Attendance.GetStudentAttendance;
using SchoMans.Application.Features.Attendance.MarkAttendance;
using SchoMans.Application.Features.Attendance.ScanQr;
using SchoMans.Application.Features.Schedules.CreateSchedule;
using SchoMans.Application.Features.Schedules.ListSchedules;
using SchoMans.Application.Features.Schedules.UpdateSchedule;
using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Tests.Validators;

/// <summary>The request rules of Phase 3: timetable, attendance, assignments.</summary>
public class ClassroomFeatureValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 2, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ---- schedules ----

    [Fact]
    public void A_lesson_needs_its_references_and_must_end_after_it_starts()
    {
        var validator = new CreateScheduleValidator();

        Assert.True(validator.Validate(new CreateScheduleCommand(Id, Id, Id, DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(8, 30))).IsValid);
        Assert.False(validator.Validate(new CreateScheduleCommand(Id, Id, Id, DayOfWeek.Monday, new TimeOnly(8, 30), new TimeOnly(8, 30))).IsValid);
        Assert.False(validator.Validate(new CreateScheduleCommand(Guid.Empty, Id, Id, DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(8, 0))).IsValid);
        Assert.False(validator.Validate(new CreateScheduleCommand(Id, Id, Id, (DayOfWeek)9, new TimeOnly(7, 0), new TimeOnly(8, 0))).IsValid);
    }

    [Fact]
    public void Updating_a_lesson_needs_a_row_version()
    {
        var validator = new UpdateScheduleValidator();

        Assert.True(validator.Validate(new UpdateScheduleCommand(Id, Id, Id, DayOfWeek.Friday, new TimeOnly(7, 0), new TimeOnly(8, 0), "AAAAAAAAB9E=")).IsValid);
        Assert.False(validator.Validate(new UpdateScheduleCommand(Id, Id, Id, DayOfWeek.Friday, new TimeOnly(7, 0), new TimeOnly(8, 0), "")).IsValid);
    }

    [Theory]
    [InlineData(1, 100, true)]
    [InlineData(1, 101, false)]
    [InlineData(0, 10, false)]
    public void The_timetable_list_pages_like_every_list(int page, int pageSize, bool valid)
    {
        Assert.Equal(valid, new ListSchedulesValidator().Validate(new ListSchedulesQuery(page, pageSize)).IsValid);
    }

    // ---- attendance ----

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(-6.2, 106.8, true)]
    [InlineData(-6.2, null, false)]
    [InlineData(null, 106.8, false)]
    [InlineData(91.0, 106.8, false)]
    [InlineData(-6.2, 181.0, false)]
    public void A_scan_sends_both_coordinates_or_neither(double? latitude, double? longitude, bool valid)
    {
        Assert.Equal(valid, new ScanQrValidator().Validate(new ScanQrCommand("token", latitude, longitude)).IsValid);
    }

    [Fact]
    public void A_scan_needs_a_token_of_reasonable_length()
    {
        Assert.False(new ScanQrValidator().Validate(new ScanQrCommand("")).IsValid);
        Assert.False(new ScanQrValidator().Validate(new ScanQrCommand(new string('x', 101))).IsValid);
    }

    [Fact]
    public void A_manual_mark_has_a_known_status_and_a_short_note()
    {
        var validator = new MarkAttendanceValidator();

        Assert.True(validator.Validate(new MarkAttendanceCommand(Id, Id, AttendanceStatus.Sick, "Flu")).IsValid);
        Assert.False(validator.Validate(new MarkAttendanceCommand(Id, Id, (AttendanceStatus)42, null)).IsValid);
        Assert.False(validator.Validate(new MarkAttendanceCommand(Id, Id, AttendanceStatus.Excused, new string('x', 301))).IsValid);
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-1", false)]
    [InlineData("2026-13", false)]
    [InlineData("10-2026", false)]
    [InlineData("", false)]
    public void A_month_looks_like_2026_10(string month, bool valid)
    {
        Assert.Equal(valid, new GetClassroomSummaryValidator().Validate(new GetClassroomSummaryQuery(Id, month)).IsValid);
        Assert.Equal(valid, new GetStudentAttendanceValidator().Validate(new GetStudentAttendanceQuery(month)).IsValid);
    }

    // ---- assignments ----

    private static CreateAssignmentCommand Assignment(
        string title = "Fractions",
        DateTimeOffset? due = null,
        IReadOnlyList<Guid>? classrooms = null,
        IReadOnlyList<NewAttachment>? attachments = null) =>
        new(title, "Page 12", due ?? Now.AddDays(1), Id, classrooms ?? [Id], attachments);

    [Fact]
    public void A_complete_assignment_is_valid()
    {
        Assert.True(new CreateAssignmentValidator(new FixedTime(Now)).Validate(Assignment()).IsValid);
    }

    [Fact]
    public void An_assignment_is_due_in_the_future_for_at_least_one_classroom_with_a_title()
    {
        var validator = new CreateAssignmentValidator(new FixedTime(Now));

        Assert.False(validator.Validate(Assignment(due: Now)).IsValid);
        Assert.False(validator.Validate(Assignment(classrooms: [])).IsValid);
        Assert.False(validator.Validate(Assignment(title: " ")).IsValid);
        Assert.False(validator.Validate(Assignment(title: new string('t', 201))).IsValid);
    }

    [Fact]
    public void An_assignment_has_at_most_five_attachments_with_names()
    {
        var validator = new CreateAssignmentValidator(new FixedTime(Now));
        var five = Enumerable.Range(0, 5).Select(i => new NewAttachment($"k{i}", $"f{i}.pdf")).ToList();

        Assert.True(validator.Validate(Assignment(attachments: five)).IsValid);
        Assert.False(validator.Validate(Assignment(attachments: [.. five, new NewAttachment("k5", "f5.pdf")])).IsValid);
        Assert.False(validator.Validate(Assignment(attachments: [new NewAttachment("k", " ")])).IsValid);
    }

    [Fact]
    public void An_update_counts_kept_and_new_attachments_together()
    {
        var validator = new UpdateAssignmentValidator();
        var three = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var news = Enumerable.Range(0, 3).Select(i => new NewAttachment($"k{i}", $"f{i}.pdf")).ToList();

        Assert.True(validator.Validate(new UpdateAssignmentCommand(Id, "T", null, Now, [Id], three, news[..2], "AAAAAAAAB9E=")).IsValid);
        Assert.False(validator.Validate(new UpdateAssignmentCommand(Id, "T", null, Now, [Id], three, news, "AAAAAAAAB9E=")).IsValid);
        Assert.False(validator.Validate(new UpdateAssignmentCommand(Id, "T", null, Now, [Id], null, null, "")).IsValid);
    }

    [Theory]
    [InlineData("application/pdf", 1, true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", 10 * 1024 * 1024, true)]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation", 100, true)]
    [InlineData("application/zip", 100, false)]
    [InlineData("application/pdf", 10 * 1024 * 1024 + 1, false)]
    [InlineData("application/pdf", 0, false)]
    public void Teachers_attach_pdfs_images_and_office_files_of_at_most_10_mb(string type, long size, bool valid)
    {
        Assert.Equal(valid, new CreateAttachmentUploadUrlValidator().Validate(new CreateAttachmentUploadUrlCommand(type, size)).IsValid);
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", false)]
    [InlineData("text/html", false)]
    public void Students_hand_in_a_pdf_or_an_image(string type, bool valid)
    {
        Assert.Equal(valid, new CreateSubmissionUploadUrlValidator().Validate(new CreateSubmissionUploadUrlCommand(Id, type, 1024)).IsValid);
        Assert.Equal(valid, AssignmentFileRules.SubmissionTypes.ContainsKey(type));
    }

    [Theory]
    [InlineData("key", "a.pdf", null, true)]
    [InlineData(null, null, "My answer", true)]
    [InlineData("key", "a.pdf", "and a note", true)]
    [InlineData(null, null, "  ", false)]
    [InlineData("key", null, null, false)]
    public void Handed_in_work_is_a_named_file_or_a_text(string? key, string? name, string? text, bool valid)
    {
        Assert.Equal(valid, new SubmitAssignmentValidator().Validate(new SubmitAssignmentCommand(Id, key, name, text)).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(88.25, true)]
    [InlineData(88.125, false)]
    [InlineData(100.5, false)]
    [InlineData(-1, false)]
    public void A_score_is_0_to_100_with_two_decimals(decimal score, bool valid)
    {
        Assert.Equal(valid, new GradeSubmissionValidator().Validate(new GradeSubmissionCommand(Id, score, null)).IsValid);
    }
}
