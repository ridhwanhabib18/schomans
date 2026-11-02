using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.Admissions;
using SchoMans.Application.Features.Admissions.AnnounceResults;
using SchoMans.Application.Features.Admissions.ApplicantDocuments;
using SchoMans.Application.Features.Admissions.Periods;
using SchoMans.Application.Features.Admissions.SaveApplicantForm;
using SchoMans.Application.Features.Admissions.Staff;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.Students;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// "Admission flows pass": the applicant registers, uploads documents and hands in; staff verify, decide and enroll; the
/// results become visible together at the announcement moment. The admission period is global (two may not be open at once),
/// so this class has a database of its own and every test first retires the periods of the one before.
/// </summary>
public class AdmissionEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/admissions";

    private static readonly ApplicantFormDto Form = new("Rina Putri", new DateOnly(2013, 5, 17), "Bandung", "Jl. Merdeka 1", "Agus Putra", "+62 812-3456", "SD Negeri 1");

    private static DateTimeOffset Days(double days) => DateTimeOffset.UtcNow.AddDays(days);

    // Every retired period gets a window of the past of its own, so that not even old periods overlap each other.
    private static int _retiredWindows;

    private async Task<Guid> ActiveYearIdAsync(HttpClient admin)
    {
        var years = await (await admin.GetAsync("/api/v1/academic-years?pageSize=100")).ReadAsync<PagedResult<AcademicYearDto>>();
        return years.Items.Single(y => y.IsActive).Id;
    }

    /// <summary>Moves every period out of the way (all in the past), then makes one that is open now and announces its results in two days.</summary>
    private async Task<AdmissionPeriodDto> FreshOpenPeriodAsync(HttpClient admin)
    {
        await RetireAllPeriodsAsync(admin);
        var created = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(
            await ActiveYearIdAsync(admin), $"Intake {Guid.NewGuid():N}"[..20], Days(-1), Days(1), Days(2), Days(10)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.ReadAsync<AdmissionPeriodDto>();
    }

    private async Task RetireAllPeriodsAsync(HttpClient admin)
    {
        var periods = await (await admin.GetAsync($"{Url}/periods")).ReadAsync<List<AdmissionPeriodDto>>();
        // Each one gets a window of its own in the past: two periods may not overlap, not even old ones.
        var recent = periods.Where(p => p.ClosesAt > DateTimeOffset.UtcNow.AddDays(-30)).ToList();
        foreach (var period in recent)
        {
            var shift = 200 + (30 * Interlocked.Increment(ref _retiredWindows));
            (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-shift), Days(-shift + 10), Days(-shift + 20), Days(-shift + 25)))).EnsureSuccessStatusCode();
        }
    }

    private static UpdateAdmissionPeriodCommand Moved(AdmissionPeriodDto period, DateTimeOffset opens, DateTimeOffset closes, DateTimeOffset results, DateTimeOffset deadline) =>
        new(period.Id, period.Name, opens, closes, results, deadline);

    private HttpClient NewApplicant(out string sub, out string email)
    {
        var id = Guid.NewGuid().ToString("N");
        sub = $"sub-{id}";
        email = $"applicant-{id}@test.example";
        return fixture.AsUser(email, "Dewi Lestari", sub, "Applicant");
    }

    private async Task<MyApplicationDto> UploadAsync(HttpClient applicant, DocumentType type, string contentType = "application/pdf")
    {
        var upload = await (await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(type, contentType, 4096))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Private, upload.Key, 4096, contentType);
        var attached = await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(type, upload.Key, $"{type}.pdf"));
        Assert.Equal(HttpStatusCode.OK, attached.StatusCode);
        return await attached.ReadAsync<MyApplicationDto>();
    }

    /// <summary>The applicant starts, fills in the form, uploads the three documents and hands in.</summary>
    private async Task<MyApplicationDto> SubmittedApplicationAsync(HttpClient applicant)
    {
        (await applicant.PostAsync($"{Url}/me", null)).EnsureSuccessStatusCode();
        (await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form))).EnsureSuccessStatusCode();
        foreach (var type in Enum.GetValues<DocumentType>())
        {
            await UploadAsync(applicant, type);
        }

        var submitted = await applicant.PostAsync($"{Url}/me/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        return await submitted.ReadAsync<MyApplicationDto>();
    }

    private static async Task VerifyAllAsync(HttpClient admin, Guid applicantId)
    {
        foreach (var type in Enum.GetValues<DocumentType>())
        {
            var reviewed = await admin.PutJsonAsync($"{Url}/applicants/{applicantId}/documents/{type}/review", new ReviewDocumentCommand(applicantId, type, true, null));
            Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        }
    }

    // ---- the public banner ----

    [Fact]
    public async Task Anybody_sees_the_period_that_is_open_or_the_next_one()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        await RetireAllPeriodsAsync(admin);

        var none = await (await guest.GetAsync($"{Url}/open")).ReadAsync<OpenAdmissionDto>();
        var upcoming = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(await ActiveYearIdAsync(admin), "Next intake", Days(5), Days(20), Days(25), Days(40)));
        var next = await (await guest.GetAsync($"{Url}/open")).ReadAsync<OpenAdmissionDto>();
        var open = await FreshOpenPeriodAsync(admin);
        var now = await (await guest.GetAsync($"{Url}/open")).ReadAsync<OpenAdmissionDto>();

        Assert.Null(none.Period);
        Assert.Equal(HttpStatusCode.Created, upcoming.StatusCode);
        Assert.False(next.Period!.IsOpen);
        Assert.Equal("Next intake", next.Period.Name);
        Assert.True(now.Period!.IsOpen);
        Assert.Equal(open.Id, now.Period.Id);
    }

    // ---- the whole flow ----

    [Fact]
    public async Task An_applicant_is_accepted_sees_it_only_at_the_announcement_confirms_and_is_enrolled_as_a_student()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out var sub, out var email);
        var classroom = await TestData.ClassroomAsync(admin, "Class A");
        var nisn = TestData.NextNisn();

        var submitted = await SubmittedApplicationAsync(applicant);
        var list = await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>();
        var row = Assert.Single(list.Items);
        var detail = await (await admin.GetAsync($"{Url}/applicants/{row.Id}")).ReadAsync<ApplicantDetailDto>();
        var documentUrl = await admin.GetAsync($"{Url}/applicants/{row.Id}/documents/FamilyCard/url");
        await VerifyAllAsync(admin, row.Id);
        var accepted = await admin.PostJsonAsync($"{Url}/applicants/{row.Id}/decision", new DecideApplicantCommand(row.Id, true, "Welcome to the school."));
        var hidden = await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>();

        Assert.Equal(ApplicantStatus.Submitted, submitted.Status);
        Assert.False(submitted.CanEdit);
        Assert.Equal((ApplicantStatus.Submitted, 0, 3, email), (row.Status, row.VerifiedDocuments, row.TotalDocuments, row.Email));
        Assert.Equal("Rina Putri", row.FullName);
        Assert.Equal(Form, detail.Form);
        Assert.Equal(HttpStatusCode.OK, documentUrl.StatusCode);
        Assert.Contains("admissions/", (await documentUrl.ReadAsync<SchoMans.Application.Features.Assignments.DownloadUrlDto>()).Url);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // Staff decided, but the applicant sees nothing of it until the results are announced.
        Assert.Equal(ApplicantStatus.InVerification, hidden.Application!.Status);
        Assert.Null(hidden.Application.Notes);
        Assert.False(hidden.Application.ResultsVisible);
        Assert.False(hidden.Application.CanConfirmReregistration);
        var tooEarly = await applicant.PostAsync($"{Url}/me/reregistration", null);
        Assert.Equal("admissions.not_accepted", await tooEarly.ProblemCodeAsync());

        // The announcement moment comes: everybody sees the result at once.
        (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-10), Days(-3), Days(-1), Days(10)))).EnsureSuccessStatusCode();
        var visible = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!;
        Assert.Equal((ApplicantStatus.Accepted, "Welcome to the school.", true), (visible.Status, visible.Notes, visible.CanConfirmReregistration));

        var notEnrolled = await admin.PostJsonAsync($"{Url}/applicants/{row.Id}/enroll", new EnrollApplicantCommand(row.Id, classroom.Id, nisn));
        Assert.Equal("admissions.not_confirmed", await notEnrolled.ProblemCodeAsync());

        var confirmed = await applicant.PostAsync($"{Url}/me/reregistration", null);
        var again = await applicant.PostAsync($"{Url}/me/reregistration", null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var enrolled = await admin.PostJsonAsync($"{Url}/applicants/{row.Id}/enroll", new EnrollApplicantCommand(row.Id, classroom.Id, nisn));
        var after = await enrolled.ReadAsync<ApplicantDetailDto>();
        var students = await (await admin.GetAsync($"/api/v1/students?search={nisn}")).ReadAsync<PagedResult<StudentDto>>();

        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode);
        Assert.Equal(ApplicantStatus.Enrolled, after.Status);
        Assert.Contains(sub, fixture.Identity.Promoted);
        var student = Assert.Single(students.Items);
        Assert.Equal((email, classroom.Id), (student.Email, student.ClassroomId));
        Assert.Equal(after.StudentId, student.Id);
        Assert.Equal(UserRole.Student, await RoleOfAsync(email));
    }

    [Fact]
    public async Task A_failing_identity_provider_stops_the_enrollment_and_changes_nothing()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out var email);
        var classroom = await TestData.ClassroomAsync(admin, "Class A");
        var nisn = TestData.NextNisn();
        await SubmittedApplicationAsync(applicant);
        var id = (await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>()).Items.Single().Id;
        await VerifyAllAsync(admin, id);
        (await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null))).EnsureSuccessStatusCode();
        (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-10), Days(-3), Days(-1), Days(10)))).EnsureSuccessStatusCode();
        (await applicant.PostAsync($"{Url}/me/reregistration", null)).EnsureSuccessStatusCode();

        fixture.Identity.Fail = true;
        try
        {
            var failed = await admin.PostJsonAsync($"{Url}/applicants/{id}/enroll", new EnrollApplicantCommand(id, classroom.Id, nisn));
            Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
            Assert.Equal("admissions.identity_failed", await failed.ProblemCodeAsync());
        }
        finally
        {
            fixture.Identity.Fail = false;
        }

        var detail = await (await admin.GetAsync($"{Url}/applicants/{id}")).ReadAsync<ApplicantDetailDto>();
        var students = await (await admin.GetAsync($"/api/v1/students?search={nisn}")).ReadAsync<PagedResult<StudentDto>>();
        Assert.Equal(ApplicantStatus.Accepted, detail.Status);
        Assert.Null(detail.StudentId);
        Assert.Empty(students.Items);
        Assert.Equal(UserRole.Applicant, await RoleOfAsync(email));
    }

    // ---- documents ----

    [Fact]
    public async Task A_rejected_document_is_uploaded_again_and_only_that_one_while_the_application_is_in_verification()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        await SubmittedApplicationAsync(applicant);
        var id = (await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>()).Items.Single().Id;

        var rejected = await admin.PutJsonAsync($"{Url}/applicants/{id}/documents/ReportCard/review", new ReviewDocumentCommand(id, DocumentType.ReportCard, false, "The photo is blurred."));
        var mine = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!;
        var notices = await (await applicant.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(ApplicantStatus.InVerification, mine.Status);
        var report = mine.Documents.Single(d => d.Type == DocumentType.ReportCard);
        Assert.Equal((DocumentStatus.Rejected, "The photo is blurred."), (report.Status, report.ReviewNote));
        Assert.True(mine.CanReplaceRejectedDocuments);
        Assert.Contains(notices.Items, n => n.Title == "A document needs a new upload" && n.Body == "The photo is blurred." && n.LinkUrl == "/admissions");

        // Only the rejected document can be replaced now.
        var locked = await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(DocumentType.FamilyCard, "application/pdf", 100));
        Assert.Equal("admissions.document_locked", await locked.ProblemCodeAsync());

        var replaced = await UploadAsync(applicant, DocumentType.ReportCard, "image/png");
        var again = replaced.Documents.Single(d => d.Type == DocumentType.ReportCard);
        Assert.Equal((DocumentStatus.Pending, null), (again.Status, again.ReviewNote));
    }

    [Fact]
    public async Task A_file_must_be_the_applicant_s_own_upload_of_the_right_kind_and_really_uploaded()
    {
        using var admin = fixture.AsAdmin();
        await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        (await applicant.PostAsync($"{Url}/me", null)).EnsureSuccessStatusCode();
        var notUploaded = (await (await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(DocumentType.FamilyCard, "application/pdf", 10))).ReadAsync<UploadUrlDto>()).Key;
        var wrongKind = (await (await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(DocumentType.BirthCertificate, "application/pdf", 10))).ReadAsync<UploadUrlDto>()).Key;
        fixture.Storage.Put(FileBucket.Private, wrongKind, 10, "application/pdf");
        var tooBig = (await (await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(DocumentType.ReportCard, "application/pdf", 10))).ReadAsync<UploadUrlDto>()).Key;
        fixture.Storage.Put(FileBucket.Private, tooBig, 11 * 1024 * 1024, "application/pdf");

        var missing = await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(DocumentType.FamilyCard, notUploaded, "kk.pdf"));
        var foreign = await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(DocumentType.FamilyCard, "admissions/someone-else/FamilyCard/x.pdf", "kk.pdf"));
        var kind = await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(DocumentType.FamilyCard, wrongKind, "kk.pdf"));
        var big = await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(DocumentType.ReportCard, tooBig, "rapor.pdf"));
        var badType = await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(DocumentType.FamilyCard, "application/x-msdownload", 10));

        Assert.Equal("files.not_uploaded", await missing.ProblemCodeAsync());
        Assert.Equal("files.invalid_key", await foreign.ProblemCodeAsync());
        Assert.Equal("files.invalid_key", await kind.ProblemCodeAsync());
        Assert.Equal("files.invalid_file", await big.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
    }

    // ---- rules of the application ----

    [Fact]
    public async Task An_incomplete_application_is_not_handed_in_and_nothing_starts_without_an_open_period()
    {
        using var admin = fixture.AsAdmin();
        await RetireAllPeriodsAsync(admin);
        using var early = NewApplicant(out _, out _);
        var closed = await early.PostAsync($"{Url}/me", null);
        Assert.Equal((HttpStatusCode.Conflict, "admissions.not_open"), (closed.StatusCode, await closed.ProblemCodeAsync()));

        await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        (await applicant.PostAsync($"{Url}/me", null)).EnsureSuccessStatusCode();
        var noForm = await applicant.PostAsync($"{Url}/me/submit", null);
        (await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form))).EnsureSuccessStatusCode();
        var noDocuments = await applicant.PostAsync($"{Url}/me/submit", null);
        var invalidForm = await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form with { GuardianPhone = "call me" }));

        Assert.Equal("admissions.form_incomplete", await noForm.ProblemCodeAsync());
        Assert.Equal("admissions.documents_missing", await noDocuments.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalidForm.StatusCode);
    }

    [Fact]
    public async Task Starting_twice_gives_the_same_application_and_a_handed_in_form_is_final()
    {
        using var admin = fixture.AsAdmin();
        await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);

        var first = await (await applicant.PostAsync($"{Url}/me", null)).ReadAsync<MyApplicationDto>();
        var second = await (await applicant.PostAsync($"{Url}/me", null)).ReadAsync<MyApplicationDto>();
        await SubmittedApplicationAsync2(applicant);
        var changed = await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form with { Address = "Elsewhere" }));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Dewi Lestari", first.Form.FullName); // the form starts with the name of the account
        Assert.Equal((HttpStatusCode.Conflict, "admissions.not_editable"), (changed.StatusCode, await changed.ProblemCodeAsync()));
    }

    /// <summary>Like <see cref="SubmittedApplicationAsync"/> for an applicant who has already started.</summary>
    private async Task SubmittedApplicationAsync2(HttpClient applicant)
    {
        (await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form))).EnsureSuccessStatusCode();
        foreach (var type in Enum.GetValues<DocumentType>())
        {
            await UploadAsync(applicant, type);
        }

        (await applicant.PostAsync($"{Url}/me/submit", null)).EnsureSuccessStatusCode();
    }

    // ---- decisions ----

    [Fact]
    public async Task Staff_accept_only_verified_applications_and_reject_with_a_reason()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        await SubmittedApplicationAsync(applicant);
        var id = (await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>()).Items.Single().Id;

        var unverified = await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null));
        var noReason = await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, false, null));
        var rejected = await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, false, "The quota is full."));
        var detail = await rejected.ReadAsync<ApplicantDetailDto>();
        var late = await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null));
        var hidden = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!;

        Assert.Equal("admissions.documents_not_verified", await unverified.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal((ApplicantStatus.Rejected, "The quota is full."), (detail.Status, detail.Notes));
        Assert.Equal("admissions.not_in_review", await late.ProblemCodeAsync());
        Assert.Equal((ApplicantStatus.InVerification, null), (hidden.Status, hidden.Notes)); // until the announcement
    }

    [Fact]
    public async Task Enrolling_needs_a_free_nisn_and_a_classroom_that_exists()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        await SubmittedApplicationAsync(applicant);
        var id = (await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>()).Items.Single().Id;
        await VerifyAllAsync(admin, id);
        (await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null))).EnsureSuccessStatusCode();
        (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-10), Days(-3), Days(-1), Days(10)))).EnsureSuccessStatusCode();
        (await applicant.PostAsync($"{Url}/me/reregistration", null)).EnsureSuccessStatusCode();
        var classroom = await TestData.ClassroomAsync(admin, "Class A");
        var taken = (await TestData.CreateStudentAsync(admin, classroom.Id)).Nisn;

        var duplicate = await admin.PostJsonAsync($"{Url}/applicants/{id}/enroll", new EnrollApplicantCommand(id, classroom.Id, taken));
        var noClassroom = await admin.PostJsonAsync($"{Url}/applicants/{id}/enroll", new EnrollApplicantCommand(id, Guid.NewGuid(), TestData.NextNisn()));
        var badNisn = await admin.PostJsonAsync($"{Url}/applicants/{id}/enroll", new EnrollApplicantCommand(id, classroom.Id, "123"));

        Assert.Equal("students.nisn_taken", await duplicate.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, noClassroom.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badNisn.StatusCode);
    }

    [Fact]
    public async Task The_place_cannot_be_confirmed_after_the_deadline()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        await SubmittedApplicationAsync(applicant);
        var id = (await (await admin.GetAsync($"{Url}/applicants?periodId={period.Id}")).ReadAsync<PagedResult<ApplicantListItemDto>>()).Items.Single().Id;
        await VerifyAllAsync(admin, id);
        (await admin.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null))).EnsureSuccessStatusCode();
        (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-20), Days(-15), Days(-10), Days(-1)))).EnsureSuccessStatusCode();

        var late = await applicant.PostAsync($"{Url}/me/reregistration", null);

        Assert.Equal((HttpStatusCode.Conflict, "admissions.reregistration_closed"), (late.StatusCode, await late.ProblemCodeAsync()));
    }

    // ---- the announcement job ----

    [Fact]
    public async Task The_results_job_tells_the_decided_applicants_once_when_the_announcement_moment_has_come()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        await RunResultsJobAsync(); // the periods of the other tests are over too: the job catches up on them first
        using var accepted = NewApplicant(out _, out _);
        using var rejected = NewApplicant(out _, out _);
        using var undecided = NewApplicant(out _, out _);
        foreach (var client in new[] { accepted, rejected, undecided })
        {
            await SubmittedApplicationAsync(client);
        }

        var acceptedId = await IdOfAsync(accepted);
        var rejectedId = await IdOfAsync(rejected);
        await VerifyAllAsync(admin, acceptedId);
        (await admin.PostJsonAsync($"{Url}/applicants/{acceptedId}/decision", new DecideApplicantCommand(acceptedId, true, null))).EnsureSuccessStatusCode();
        (await admin.PostJsonAsync($"{Url}/applicants/{rejectedId}/decision", new DecideApplicantCommand(rejectedId, false, "Sorry."))).EnsureSuccessStatusCode();

        var before = await RunResultsJobAsync();
        (await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(-10), Days(-3), Days(-1), Days(10)))).EnsureSuccessStatusCode();
        var first = await RunResultsJobAsync();
        var second = await RunResultsJobAsync();

        var acceptedNotices = await (await accepted.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();
        var rejectedNotices = await (await rejected.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();
        var undecidedNotices = await (await undecided.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.Equal(0, before);
        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Single(acceptedNotices.Items, n => n.Title == "You are accepted");
        Assert.Single(rejectedNotices.Items, n => n.Title == "The result of your application is out");
        Assert.DoesNotContain(undecidedNotices.Items, n => n.LinkUrl == "/admissions" && n.Title.Contains("result", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int> RunResultsJobAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AnnounceAdmissionResultsHandler>().Handle(CancellationToken.None);
    }

    // ---- periods ----

    [Fact]
    public async Task The_admin_manages_the_periods_and_two_may_not_be_open_at_once()
    {
        using var admin = fixture.AsAdmin();
        await RetireAllPeriodsAsync(admin);
        var year = await ActiveYearIdAsync(admin);

        var created = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(year, "Intake A", Days(10), Days(20), Days(25), Days(40)));
        var period = await created.ReadAsync<AdmissionPeriodDto>();
        var overlapping = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(year, "Intake B", Days(15), Days(30), Days(35), Days(50)));
        var updated = await admin.PutJsonAsync($"{Url}/periods/{period.Id}", Moved(period, Days(11), Days(21), Days(26), Days(41)));
        var unknownYear = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(Guid.NewGuid(), "Intake C", Days(100), Days(110), Days(115), Days(130)));
        var badOrder = await admin.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(year, "Intake D", Days(100), Days(90), Days(115), Days(130)));
        var deleted = await admin.DeleteAsync($"{Url}/periods/{period.Id}");
        var gone = await admin.DeleteAsync($"{Url}/periods/{period.Id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal((HttpStatusCode.Conflict, "admissions.period_overlaps"), (overlapping.StatusCode, await overlapping.ProblemCodeAsync()));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownYear.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badOrder.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task A_period_with_applications_cannot_be_deleted()
    {
        using var admin = fixture.AsAdmin();
        var period = await FreshOpenPeriodAsync(admin);
        using var applicant = NewApplicant(out _, out _);
        (await applicant.PostAsync($"{Url}/me", null)).EnsureSuccessStatusCode();

        var response = await admin.DeleteAsync($"{Url}/periods/{period.Id}");
        var listed = (await (await admin.GetAsync($"{Url}/periods")).ReadAsync<List<AdmissionPeriodDto>>()).Single(p => p.Id == period.Id);

        Assert.Equal((HttpStatusCode.Conflict, "admissions.period_in_use"), (response.StatusCode, await response.ProblemCodeAsync()));
        Assert.Equal(1, listed.ApplicantCount);
    }

    // ---- who may do what ----

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    [InlineData("Principal")]
    public async Task Only_applicants_have_an_application(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/me", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/me/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(Form))).StatusCode);
    }

    [Theory]
    [InlineData("Applicant")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Teacher")]
    [InlineData("Principal")]
    public async Task Only_admin_staff_manage_admissions(string role)
    {
        using var client = fixture.AsNewUser(role);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/periods")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/applicants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/applicants/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/applicants/{id}/decision", new DecideApplicantCommand(id, true, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/applicants/{id}/enroll", new EnrollApplicantCommand(id, id, "1234567890"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/periods", new CreateAdmissionPeriodCommand(id, "X", Days(1), Days(2), Days(3), Days(4)))).StatusCode);
    }

    [Fact]
    public async Task The_banner_needs_no_account_but_everything_else_needs_a_token()
    {
        using var guest = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"{Url}/open")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}/periods")).StatusCode);
    }

    private async Task<Guid> IdOfAsync(HttpClient applicant)
    {
        var mine = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!;
        return mine.Id;
    }

    private async Task<UserRole> RoleOfAsync(string email)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Role).SingleAsync();
    }
}
