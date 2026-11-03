using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Admissions;
using SchoMans.Application.Features.Admissions.ApplicantDocuments;
using SchoMans.Application.Features.Admissions.ExtractDocuments;
using SchoMans.Application.Features.Admissions.SaveApplicantForm;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Domain.Admissions;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>AI-03: the AI reads the photos of the admission documents; the applicant confirms, staff see what disagrees.</summary>
[Collection(ApiCollection.Name)]
public class DocumentExtractionEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/admissions";

    /// <summary>Starts an application in the open period (the demo data has one) and returns the applicant's client.</summary>
    private async Task<HttpClient> ApplicantAsync(ApplicantFormDto? form = null)
    {
        using var admin = fixture.AsAdmin();
        var open = await (await fixture.Factory.CreateClient().GetAsync($"{Url}/open")).ReadAsync<OpenAdmissionDto>();
        if (open.Period is not { IsOpen: true })
        {
            var years = await (await admin.GetAsync("/api/v1/academic-years?pageSize=100")).ReadAsync<SchoMans.Application.Common.PagedResult<SchoMans.Application.Features.AcademicYears.AcademicYearDto>>();
            var now = DateTimeOffset.UtcNow;
            (await admin.PostJsonAsync($"{Url}/periods", new SchoMans.Application.Features.Admissions.Periods.CreateAdmissionPeriodCommand(
                years.Items.Single(y => y.IsActive).Id, $"Intake {Guid.NewGuid():N}"[..20], now.AddDays(-1), now.AddDays(5), now.AddDays(6), now.AddDays(20)))).EnsureSuccessStatusCode();
        }

        var applicant = fixture.AsNewUser("Applicant");
        (await applicant.PostAsync($"{Url}/me", null)).EnsureSuccessStatusCode();
        if (form is not null)
        {
            (await applicant.PutJsonAsync($"{Url}/me/form", new SaveApplicantFormCommand(form))).EnsureSuccessStatusCode();
        }

        return applicant;
    }

    private async Task UploadAsync(HttpClient applicant, DocumentType type, string contentType = "image/png", string? fileName = null, byte[]? content = null)
    {
        var upload = await (await applicant.PostJsonAsync($"{Url}/me/documents/upload-url", new CreateDocumentUploadUrlCommand(type, contentType, 4096))).ReadAsync<UploadUrlDto>();
        if (content is null)
        {
            fixture.Storage.Put(FileBucket.Private, upload.Key, 4096, contentType);
        }
        else
        {
            fixture.Storage.PutContent(FileBucket.Private, upload.Key, content, contentType);
        }

        (await applicant.PutJsonAsync($"{Url}/me/documents", new AttachDocumentCommand(type, upload.Key, fileName ?? $"{type}.png"))).EnsureSuccessStatusCode();
    }

    private static async Task<DocumentExtractionDto> ExtractAsync(HttpClient applicant, DocumentType type)
    {
        var response = await applicant.PostAsync($"{Url}/me/documents/{type}/extract", null);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<DocumentExtractionDto>();
    }

    private static Task<ApplicantExtractionsDto> ExtractionsAsync(HttpClient client, string path = "me/extractions") =>
        client.GetAsync($"{Url}/{path}").ContinueWith(t => t.Result.ReadAsync<ApplicantExtractionsDto>()).Unwrap();

    // ---- the applicant ----

    [Fact]
    public async Task An_applicant_has_the_ai_read_a_photo_and_the_result_is_kept_on_the_document()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.FamilyCard);

        var read = await ExtractAsync(applicant, DocumentType.FamilyCard);
        var kept = await ExtractionsAsync(applicant);

        Assert.Equal("Rina Putri Lestari", read.Fields.FullName);
        Assert.Equal("3201014505140001", read.Fields.Nik);
        Assert.Equal(new DateOnly(2014, 5, 5), read.Fields.DateOfBirth);
        Assert.Equal("Jl. Merdeka No. 10, Bandung", read.Fields.Address);
        Assert.Equal("Budi Santoso", read.Fields.GuardianName);
        var document = Assert.Single(kept.Documents);
        Assert.Equal((DocumentType.FamilyCard, read.Fields.FullName), (document.Type, document.Fields.FullName));
        // a new application starts with the account name on the form, which is not the name on the document
        Assert.All(kept.Mismatches, m => Assert.Equal("fullName", m.Field));
    }

    [Fact]
    public async Task A_report_card_gives_its_grades()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.ReportCard, "image/jpeg", "rapor.jpg");

        var read = await ExtractAsync(applicant, DocumentType.ReportCard);

        Assert.Equal(["Mathematics", "English", "Natural Sciences"], read.Fields.Grades.Select(g => g.Subject));
        Assert.Null(read.Fields.Nik);
    }

    [Fact]
    public async Task Extracting_does_not_change_the_application_form()
    {
        var form = new ApplicantFormDto("Rina Putri", new DateOnly(2014, 5, 5), "Bandung", "Jl. Merdeka 1", "Agus Putra", "+62 812-3456", "SD Negeri 1");
        using var applicant = await ApplicantAsync(form);
        await UploadAsync(applicant, DocumentType.FamilyCard);

        await ExtractAsync(applicant, DocumentType.FamilyCard);

        var mine = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!;
        Assert.Equal(form, mine.Form);
    }

    [Fact]
    public async Task Documents_and_the_form_that_disagree_are_flagged()
    {
        var form = new ApplicantFormDto("Rina Putri", new DateOnly(2013, 5, 17), "Bandung", "Jl. Merdeka 1", "Agus Putra", "+62 812-3456", "SD Negeri 1");
        using var applicant = await ApplicantAsync(form);
        await UploadAsync(applicant, DocumentType.BirthCertificate, fileName: "akta-mismatch.png"); // the mock reads another date of birth from it
        await UploadAsync(applicant, DocumentType.FamilyCard);
        await ExtractAsync(applicant, DocumentType.BirthCertificate);
        await ExtractAsync(applicant, DocumentType.FamilyCard);

        var result = await ExtractionsAsync(applicant);

        Assert.Equal(2, result.Documents.Count);
        var birth = Assert.Single(result.Mismatches, m => m.Field == "dateOfBirth");
        Assert.Equal([("Form", "2013-05-17"), ("BirthCertificate", "2014-06-17"), ("FamilyCard", "2014-05-05")], birth.Values.Select(v => (v.Source, v.Value)));
        Assert.Contains(result.Mismatches, m => m.Field == "fullName"); // "Rina Putri" on the form, "Rina Putri Lestari" on the documents
    }

    [Fact]
    public async Task A_new_file_clears_what_was_read_from_the_old_one()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.FamilyCard);
        await ExtractAsync(applicant, DocumentType.FamilyCard);

        await UploadAsync(applicant, DocumentType.FamilyCard);

        Assert.Empty((await ExtractionsAsync(applicant)).Documents);
    }

    [Fact]
    public async Task A_pdf_and_a_photo_that_is_too_large_are_refused_with_a_reason()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.BirthCertificate, "application/pdf", "akta.pdf");
        await UploadAsync(applicant, DocumentType.FamilyCard, content: new byte[DocumentExtractor.MaxImageBytes + 1]);

        var pdf = await applicant.PostAsync($"{Url}/me/documents/BirthCertificate/extract", null);
        var large = await applicant.PostAsync($"{Url}/me/documents/FamilyCard/extract", null);

        Assert.Equal((HttpStatusCode.BadRequest, "admissions.extract_needs_image"), (pdf.StatusCode, await pdf.ProblemCodeAsync()));
        Assert.Equal((HttpStatusCode.BadRequest, "admissions.extract_too_large"), (large.StatusCode, await large.ProblemCodeAsync()));
    }

    [Fact]
    public async Task A_document_that_was_not_uploaded_or_an_application_that_was_not_started_is_not_found()
    {
        using var applicant = await ApplicantAsync();
        using var nobody = fixture.AsNewUser("Applicant");

        var missing = await applicant.PostAsync($"{Url}/me/documents/ReportCard/extract", null);
        var noApplication = await nobody.PostAsync($"{Url}/me/documents/ReportCard/extract", null);
        var noApplicationList = await nobody.GetAsync($"{Url}/me/extractions");

        Assert.Equal((HttpStatusCode.NotFound, "admissions.document_not_found"), (missing.StatusCode, await missing.ProblemCodeAsync()));
        Assert.Equal((HttpStatusCode.NotFound, "admissions.no_application"), (noApplication.StatusCode, await noApplication.ProblemCodeAsync()));
        Assert.Equal(HttpStatusCode.NotFound, noApplicationList.StatusCode);
    }

    [Fact]
    public async Task A_reading_counts_as_one_ai_call_of_the_applicant_and_one_applicant_cannot_read_anothers_document()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsageLogs.Where(l => l.Feature == AiFeatures.DocumentExtraction).ExecuteDeleteAsync();
        }

        using var first = await ApplicantAsync();
        using var second = await ApplicantAsync();
        await UploadAsync(first, DocumentType.FamilyCard);

        await ExtractAsync(first, DocumentType.FamilyCard);
        var other = await second.PostAsync($"{Url}/me/documents/FamilyCard/extract", null);

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); // the second applicant has no such document of their own
        await using var check = fixture.Factory.Services.CreateAsyncScope();
        var logs = await check.ServiceProvider.GetRequiredService<AppDbContext>().AiUsageLogs.Where(l => l.Feature == AiFeatures.DocumentExtraction).ToListAsync();
        var log = Assert.Single(logs);
        Assert.False(log.IsBulk);
    }

    // ---- staff ----

    [Fact]
    public async Task Staff_have_the_ai_read_any_applicants_document_and_see_what_disagrees()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.BirthCertificate, fileName: "akta-mismatch.png");
        await UploadAsync(applicant, DocumentType.FamilyCard);
        var id = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!.Id;
        using var admin = fixture.AsAdmin();

        var first = await admin.PostAsync($"{Url}/applicants/{id}/documents/BirthCertificate/extract", null);
        var second = await admin.PostAsync($"{Url}/applicants/{id}/documents/FamilyCard/extract", null);
        var result = await ExtractionsAsync(admin, $"applicants/{id}/extractions");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, result.Documents.Count);
        Assert.Contains(result.Mismatches, m => m.Field == "dateOfBirth");
        // what staff read is also what the applicant sees on their own screen
        Assert.Equal(2, (await ExtractionsAsync(applicant)).Documents.Count);
    }

    [Fact]
    public async Task An_unknown_applicant_is_not_found_for_staff()
    {
        using var admin = fixture.AsAdmin();

        var extract = await admin.PostAsync($"{Url}/applicants/{Guid.NewGuid()}/documents/FamilyCard/extract", null);
        var list = await admin.GetAsync($"{Url}/applicants/{Guid.NewGuid()}/extractions");

        Assert.Equal((HttpStatusCode.NotFound, "admissions.applicant_not_found"), (extract.StatusCode, await extract.ProblemCodeAsync()));
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    // ---- who may ----

    [Fact]
    public async Task Only_applicants_read_their_own_documents_and_only_admin_staff_read_anyones()
    {
        using var applicant = await ApplicantAsync();
        await UploadAsync(applicant, DocumentType.FamilyCard);
        var id = (await (await applicant.GetAsync($"{Url}/me")).ReadAsync<MyAdmissionDto>()).Application!.Id;
        var notApplicants = new[] { fixture.AsTeacher(), fixture.AsStudent(), fixture.AsParent(), fixture.AsPrincipal(), fixture.AsAdmin() };

        try
        {
            foreach (var client in notApplicants)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/me/documents/FamilyCard/extract", null)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/me/extractions")).StatusCode);
            }

            foreach (var client in new[] { applicant, notApplicants[0], notApplicants[1], notApplicants[2], notApplicants[3] })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/applicants/{id}/documents/FamilyCard/extract", null)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/applicants/{id}/extractions")).StatusCode);
            }
        }
        finally
        {
            foreach (var client in notApplicants)
            {
                client.Dispose();
            }
        }
    }
}
