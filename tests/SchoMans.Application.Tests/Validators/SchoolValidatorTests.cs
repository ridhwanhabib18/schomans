using SchoMans.Application.Features.Admissions;
using SchoMans.Application.Features.Admissions.ApplicantDocuments;
using SchoMans.Application.Features.Admissions.Periods;
using SchoMans.Application.Features.Admissions.SaveApplicantForm;
using SchoMans.Application.Features.Admissions.Staff;
using SchoMans.Application.Features.Requests.Chains;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Requests.DecideRequest;
using SchoMans.Application.Features.Requests.ListRequests;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Approvals;

namespace SchoMans.Application.Tests.Validators;

/// <summary>The request rules of Phase 5: admissions, requests, the approval chains.</summary>
public class SchoolValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTimeOffset Opens = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    // ---- admission periods ----

    [Fact]
    public void A_period_has_a_name_an_academic_year_and_dates_in_order()
    {
        var validator = new CreateAdmissionPeriodValidator();

        Assert.True(validator.Validate(new CreateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new CreateAdmissionPeriodCommand(Guid.Empty, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new CreateAdmissionPeriodCommand(Id, " ", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new CreateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(-1), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new CreateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(20), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new CreateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(40))).IsValid);
    }

    [Fact]
    public void An_update_of_a_period_follows_the_same_rules()
    {
        var validator = new UpdateAdmissionPeriodValidator();

        Assert.True(validator.Validate(new UpdateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new UpdateAdmissionPeriodCommand(Id, "", Opens, Opens.AddDays(30), Opens.AddDays(40), Opens.AddDays(60))).IsValid);
        Assert.False(validator.Validate(new UpdateAdmissionPeriodCommand(Id, "Intake", Opens, Opens.AddDays(30), Opens.AddDays(20), Opens.AddDays(60))).IsValid);
    }

    // ---- applicant ----

    [Fact]
    public void The_form_may_be_a_draft_but_not_too_long_or_from_the_future()
    {
        var validator = new SaveApplicantFormValidator();
        var good = new ApplicantFormDto("Rina Putri", new DateOnly(2013, 5, 17), "Bandung", "Jl. Merdeka 1", "Agus Putra", "+62 812-3456", "SD Negeri 1");

        Assert.True(validator.Validate(new SaveApplicantFormCommand(good)).IsValid);
        Assert.True(validator.Validate(new SaveApplicantFormCommand(ApplicantFormDto.Empty)).IsValid);
        Assert.False(validator.Validate(new SaveApplicantFormCommand(good with { FullName = new string('x', 201) })).IsValid);
        Assert.False(validator.Validate(new SaveApplicantFormCommand(good with { GuardianPhone = "call me" })).IsValid);
        Assert.False(validator.Validate(new SaveApplicantFormCommand(good with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) })).IsValid);
        Assert.False(validator.Validate(new SaveApplicantFormCommand(good with { DateOfBirth = new DateOnly(1950, 1, 1) })).IsValid);
    }

    [Fact]
    public void The_form_is_complete_only_when_every_field_is_filled()
    {
        var complete = new ApplicantFormDto("Rina", new DateOnly(2013, 5, 17), "Bandung", "Jl. 1", "Agus", "0812", "SD 1");

        Assert.True(complete.IsComplete);
        Assert.False(ApplicantFormDto.Empty.IsComplete);
        Assert.False((complete with { Address = " " }).IsComplete);
        Assert.False((complete with { DateOfBirth = null }).IsComplete);
        Assert.False((complete with { PreviousSchool = "" }).IsComplete);
    }

    [Fact]
    public void A_document_is_a_pdf_or_an_image_of_at_most_10_mb()
    {
        var validator = new CreateDocumentUploadUrlValidator();

        Assert.True(validator.Validate(new CreateDocumentUploadUrlCommand(DocumentType.FamilyCard, "application/pdf", 5_000)).IsValid);
        Assert.True(validator.Validate(new CreateDocumentUploadUrlCommand(DocumentType.ReportCard, "image/png", 10 * 1024 * 1024)).IsValid);
        Assert.False(validator.Validate(new CreateDocumentUploadUrlCommand(DocumentType.ReportCard, "image/png", 10 * 1024 * 1024 + 1)).IsValid);
        Assert.False(validator.Validate(new CreateDocumentUploadUrlCommand(DocumentType.ReportCard, "application/x-msdownload", 100)).IsValid);
        Assert.False(validator.Validate(new CreateDocumentUploadUrlCommand(DocumentType.ReportCard, "image/png", 0)).IsValid);
        Assert.False(validator.Validate(new CreateDocumentUploadUrlCommand((DocumentType)9, "image/png", 10)).IsValid);
    }

    [Fact]
    public void Attaching_a_document_needs_the_key_and_a_name()
    {
        var validator = new AttachDocumentValidator();

        Assert.True(validator.Validate(new AttachDocumentCommand(DocumentType.BirthCertificate, "admissions/x/BirthCertificate/1.pdf", "akta.pdf")).IsValid);
        Assert.False(validator.Validate(new AttachDocumentCommand(DocumentType.BirthCertificate, "", "akta.pdf")).IsValid);
        Assert.False(validator.Validate(new AttachDocumentCommand(DocumentType.BirthCertificate, "key", "")).IsValid);
        Assert.False(validator.Validate(new AttachDocumentCommand(DocumentType.BirthCertificate, new string('k', 301), "akta.pdf")).IsValid);
    }

    // ---- staff ----

    [Fact]
    public void Rejecting_a_document_needs_a_note_and_verifying_does_not()
    {
        var validator = new ReviewDocumentValidator();

        Assert.True(validator.Validate(new ReviewDocumentCommand(Id, DocumentType.FamilyCard, true, null)).IsValid);
        Assert.True(validator.Validate(new ReviewDocumentCommand(Id, DocumentType.FamilyCard, false, "Too dark")).IsValid);
        Assert.False(validator.Validate(new ReviewDocumentCommand(Id, DocumentType.FamilyCard, false, null)).IsValid);
        Assert.False(validator.Validate(new ReviewDocumentCommand(Id, DocumentType.FamilyCard, false, " ")).IsValid);
        Assert.False(validator.Validate(new ReviewDocumentCommand(Id, DocumentType.FamilyCard, false, new string('x', 501))).IsValid);
    }

    [Fact]
    public void A_rejection_of_an_applicant_needs_a_reason_and_an_acceptance_does_not()
    {
        var validator = new DecideApplicantValidator();

        Assert.True(validator.Validate(new DecideApplicantCommand(Id, true, null)).IsValid);
        Assert.True(validator.Validate(new DecideApplicantCommand(Id, false, "The quota is full.")).IsValid);
        Assert.False(validator.Validate(new DecideApplicantCommand(Id, false, null)).IsValid);
        Assert.False(validator.Validate(new DecideApplicantCommand(Id, true, new string('x', 1001))).IsValid);
    }

    [Fact]
    public void Enrolling_needs_a_classroom_and_a_nisn_of_ten_digits()
    {
        var validator = new EnrollApplicantValidator();

        Assert.True(validator.Validate(new EnrollApplicantCommand(Id, Id, "1234567890")).IsValid);
        Assert.False(validator.Validate(new EnrollApplicantCommand(Id, Guid.Empty, "1234567890")).IsValid);
        Assert.False(validator.Validate(new EnrollApplicantCommand(Id, Id, "12345")).IsValid);
        Assert.False(validator.Validate(new EnrollApplicantCommand(Id, Id, "12345678ab")).IsValid);
    }

    [Fact]
    public void The_applicant_list_follows_the_uniform_paging_rules()
    {
        var validator = new ListApplicantsValidator();

        Assert.True(validator.Validate(new ListApplicantsQuery()).IsValid);
        Assert.True(validator.Validate(new ListApplicantsQuery(Id, ApplicantStatus.Accepted, "rina", 2, 100)).IsValid);
        Assert.False(validator.Validate(new ListApplicantsQuery(Page: 0)).IsValid);
        Assert.False(validator.Validate(new ListApplicantsQuery(PageSize: 101)).IsValid);
        Assert.False(validator.Validate(new ListApplicantsQuery(Status: (ApplicantStatus)9)).IsValid);
        Assert.False(validator.Validate(new ListApplicantsQuery(Search: new string('x', 101))).IsValid);
    }

    // ---- requests ----

    [Fact]
    public void A_leave_needs_a_student_and_days_that_are_in_order_and_not_too_many()
    {
        var validator = new CreateRequestValidator();
        var start = new DateOnly(2026, 11, 12);

        Assert.True(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start.AddDays(2), "Fever")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, null, start, start.AddDays(2), "Fever")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, null, start.AddDays(2), "Fever")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start.AddDays(-1), "Fever")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start.AddDays(CreateRequestValidator.MaxLeaveDays), "Fever")).IsValid);
        Assert.True(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start.AddDays(CreateRequestValidator.MaxLeaveDays - 1), "Fever")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start, " ")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.StudentLeave, Id, start, start, new string('x', Request.ReasonMaxLength + 1))).IsValid);
    }

    [Fact]
    public void A_teacher_s_leave_is_not_about_a_student_and_a_letter_has_no_days()
    {
        var validator = new CreateRequestValidator();
        var start = new DateOnly(2026, 11, 12);

        Assert.True(validator.Validate(new CreateRequestCommand(RequestType.TeacherLeave, null, start, start, "Conference")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.TeacherLeave, Id, start, start, "Conference")).IsValid);
        Assert.True(validator.Validate(new CreateRequestCommand(RequestType.CertificateLetter, Id, null, null, "For a scholarship")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand(RequestType.CertificateLetter, null, null, null, "For a scholarship")).IsValid);
        Assert.False(validator.Validate(new CreateRequestCommand((RequestType)9, Id, null, null, "x")).IsValid);
    }

    [Fact]
    public void A_rejection_needs_a_note_and_an_approval_does_not()
    {
        var validator = new DecideRequestValidator();

        Assert.True(validator.Validate(new DecideRequestCommand(Id, true, null)).IsValid);
        Assert.True(validator.Validate(new DecideRequestCommand(Id, false, "No days left")).IsValid);
        Assert.False(validator.Validate(new DecideRequestCommand(Id, false, null)).IsValid);
        Assert.False(validator.Validate(new DecideRequestCommand(Id, true, new string('x', Request.NoteMaxLength + 1))).IsValid);
    }

    [Fact]
    public void The_request_lists_follow_the_uniform_paging_rules()
    {
        Assert.True(new ListMyRequestsValidator().Validate(new ListMyRequestsQuery(RequestStatus.Pending, 1, 20)).IsValid);
        Assert.False(new ListMyRequestsValidator().Validate(new ListMyRequestsQuery(Page: 0)).IsValid);
        Assert.False(new ListInboxValidator().Validate(new ListInboxQuery(PageSize: 101)).IsValid);
        Assert.True(new ListAllRequestsValidator().Validate(new ListAllRequestsQuery(RequestStatus.Approved, RequestType.CertificateLetter)).IsValid);
        Assert.False(new ListAllRequestsValidator().Validate(new ListAllRequestsQuery(Type: (RequestType)9)).IsValid);
    }

    // ---- chains ----

    [Fact]
    public void A_chain_has_one_to_five_valid_steps()
    {
        var validator = new SetApprovalChainValidator();

        Assert.True(validator.Validate(new SetApprovalChainCommand(RequestType.StudentLeave, [ApproverRole.HomeroomTeacher, ApproverRole.Principal])).IsValid);
        Assert.True(validator.Validate(new SetApprovalChainCommand(RequestType.TeacherLeave, [ApproverRole.Admin])).IsValid);
        Assert.False(validator.Validate(new SetApprovalChainCommand(RequestType.TeacherLeave, [ApproverRole.HomeroomTeacher])).IsValid);
        Assert.False(validator.Validate(new SetApprovalChainCommand(RequestType.StudentLeave, [])).IsValid);
        Assert.False(validator.Validate(new SetApprovalChainCommand(RequestType.StudentLeave, Enumerable.Repeat(ApproverRole.Admin, 6).ToList())).IsValid);
        Assert.False(validator.Validate(new SetApprovalChainCommand(RequestType.StudentLeave, [(ApproverRole)9])).IsValid);
        Assert.False(validator.Validate(new SetApprovalChainCommand((RequestType)9, [ApproverRole.Admin])).IsValid);
    }
}
