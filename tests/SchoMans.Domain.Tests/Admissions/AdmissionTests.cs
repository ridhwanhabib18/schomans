using SchoMans.Domain.Admissions;

namespace SchoMans.Domain.Tests.Admissions;

public class AdmissionTests
{
    private static readonly DateTimeOffset Opens = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Closes = Opens.AddDays(30);
    private static readonly DateTimeOffset Results = Closes.AddDays(7);
    private static readonly DateTimeOffset Deadline = Results.AddDays(14);
    private static readonly DateTimeOffset Now = Opens.AddDays(5);

    private static AdmissionPeriod Period() => AdmissionPeriod.Create(Guid.NewGuid(), " Intake 2027 ", Opens, Closes, Results, Deadline);

    private static Applicant ApplicantWithDocuments(bool all = true)
    {
        var applicant = Applicant.Create(Guid.NewGuid(), Guid.NewGuid());
        var types = all ? Enum.GetValues<DocumentType>() : [DocumentType.BirthCertificate];
        foreach (var type in types)
        {
            applicant.Documents.Add(ApplicantDocument.Create(applicant.Id, type, $"admissions/x/{type}/1.pdf", $"{type}.pdf"));
        }

        return applicant;
    }

    private static Applicant InVerification(bool verifyAll = true)
    {
        var applicant = ApplicantWithDocuments();
        applicant.Submit(Now);
        applicant.BeginVerification();
        if (verifyAll)
        {
            applicant.Documents.ForEach(d => d.Verify());
        }

        return applicant;
    }

    // ---- period ----

    [Fact]
    public void A_period_opens_and_closes_at_its_dates()
    {
        var period = Period();

        Assert.Equal("Intake 2027", period.Name);
        Assert.False(period.IsOpenAt(Opens.AddSeconds(-1)));
        Assert.True(period.IsOpenAt(Opens));
        Assert.True(period.IsOpenAt(Closes.AddSeconds(-1)));
        Assert.False(period.IsOpenAt(Closes));
    }

    [Fact]
    public void The_results_are_visible_from_one_moment_and_the_place_can_be_confirmed_until_the_deadline()
    {
        var period = Period();

        Assert.False(period.ResultsVisibleAt(Results.AddSeconds(-1)));
        Assert.True(period.ResultsVisibleAt(Results));
        Assert.False(period.ReregistrationOpenAt(Results.AddSeconds(-1)));
        Assert.True(period.ReregistrationOpenAt(Results));
        Assert.True(period.ReregistrationOpenAt(Deadline.AddSeconds(-1)));
        Assert.False(period.ReregistrationOpenAt(Deadline));
    }

    [Fact]
    public void The_dates_of_a_period_run_in_order()
    {
        Assert.Throws<ArgumentException>(() => AdmissionPeriod.Create(Guid.NewGuid(), "X", Closes, Opens, Results, Deadline));
        Assert.Throws<ArgumentException>(() => AdmissionPeriod.Create(Guid.NewGuid(), "X", Opens, Closes, Closes, Deadline));
        Assert.Throws<ArgumentException>(() => AdmissionPeriod.Create(Guid.NewGuid(), "X", Opens, Closes, Results, Results));
        Assert.Throws<ArgumentException>(() => AdmissionPeriod.Create(Guid.NewGuid(), " ", Opens, Closes, Results, Deadline));
        Assert.Throws<ArgumentException>(() => AdmissionPeriod.Create(Guid.NewGuid(), new string('x', AdmissionPeriod.NameMaxLength + 1), Opens, Closes, Results, Deadline));
    }

    [Fact]
    public void A_period_remembers_that_the_results_were_announced()
    {
        var period = Period();
        Assert.Null(period.ResultsNotifiedAt);

        period.MarkResultsNotified(Results);

        Assert.Equal(Results, period.ResultsNotifiedAt);
    }

    // ---- documents ----

    [Fact]
    public void A_rejected_document_needs_a_note_and_a_new_file_starts_the_review_again()
    {
        var document = ApplicantDocument.Create(Guid.NewGuid(), DocumentType.FamilyCard, "k1", " kk.pdf ");
        Assert.Equal("kk.pdf", document.FileName);
        Assert.Equal(DocumentStatus.Pending, document.Status);
        Assert.Throws<ArgumentException>(() => document.Reject(" "));

        document.Reject("The photo is blurred.");
        Assert.Equal(DocumentStatus.Rejected, document.Status);
        Assert.Equal("The photo is blurred.", document.ReviewNote);

        document.Replace("k2", "kk-new.pdf");
        Assert.Equal((DocumentStatus.Pending, null, "k2"), (document.Status, document.ReviewNote, document.FileKey));

        document.Verify();
        Assert.Equal(DocumentStatus.Verified, document.Status);
    }

    // ---- applicant ----

    [Fact]
    public void An_application_is_handed_in_only_with_all_three_documents()
    {
        var incomplete = ApplicantWithDocuments(all: false);
        Assert.False(incomplete.Submit(Now));
        Assert.Equal(ApplicantStatus.Draft, incomplete.Status);

        var complete = ApplicantWithDocuments();
        Assert.True(complete.Submit(Now));
        Assert.Equal((ApplicantStatus.Submitted, Now), (complete.Status, complete.SubmittedAt));
        Assert.False(complete.Submit(Now)); // not a draft any more
    }

    [Fact]
    public void The_form_can_only_be_changed_while_the_application_is_a_draft()
    {
        var applicant = ApplicantWithDocuments();

        Assert.True(applicant.SaveForm("{\"fullName\":\"Rina\"}"));
        applicant.Submit(Now);

        Assert.False(applicant.SaveForm("{\"fullName\":\"Other\"}"));
        Assert.Equal("{\"fullName\":\"Rina\"}", applicant.FormData);
        Assert.Throws<ArgumentException>(() => Applicant.Create(Guid.NewGuid(), Guid.NewGuid()).SaveForm(new string('x', Applicant.FormDataMaxLength + 1)));
    }

    [Fact]
    public void Staff_accept_only_when_every_document_is_verified()
    {
        var unverified = InVerification(verifyAll: false);
        Assert.False(unverified.Accept("Welcome", Now));
        Assert.Equal(ApplicantStatus.InVerification, unverified.Status);

        var verified = InVerification();
        Assert.True(verified.Accept("Welcome", Now));
        Assert.Equal((ApplicantStatus.Accepted, "Welcome", Now), (verified.Status, verified.Notes, verified.DecidedAt));
    }

    [Fact]
    public void Staff_reject_with_a_note_from_a_submitted_application_but_not_from_a_draft()
    {
        var submitted = ApplicantWithDocuments();
        submitted.Submit(Now);
        Assert.Throws<ArgumentException>(() => submitted.Reject(" ", Now));
        Assert.True(submitted.Reject("The quota is full.", Now));
        Assert.Equal(ApplicantStatus.Rejected, submitted.Status);

        Assert.False(ApplicantWithDocuments().Reject("No.", Now));
    }

    [Fact]
    public void Looking_at_a_submitted_application_starts_the_verification()
    {
        var applicant = ApplicantWithDocuments();
        applicant.BeginVerification();
        Assert.Equal(ApplicantStatus.Draft, applicant.Status); // a draft is not looked at

        applicant.Submit(Now);
        applicant.BeginVerification();
        Assert.Equal(ApplicantStatus.InVerification, applicant.Status);
    }

    [Fact]
    public void A_decision_stays_hidden_until_the_results_are_announced()
    {
        var accepted = InVerification();
        accepted.Accept(null, Now);
        var rejected = ApplicantWithDocuments();
        rejected.Submit(Now);
        rejected.Reject("Sorry.", Now);

        Assert.Equal(ApplicantStatus.InVerification, accepted.VisibleStatus(resultsVisible: false));
        Assert.Equal(ApplicantStatus.InVerification, rejected.VisibleStatus(resultsVisible: false));
        Assert.Equal(ApplicantStatus.Accepted, accepted.VisibleStatus(resultsVisible: true));
        Assert.Equal(ApplicantStatus.Rejected, rejected.VisibleStatus(resultsVisible: true));
        Assert.Equal(ApplicantStatus.Draft, ApplicantWithDocuments().VisibleStatus(resultsVisible: false));
    }

    [Fact]
    public void Only_an_accepted_applicant_confirms_the_place_once_and_only_a_confirmed_place_is_enrolled()
    {
        var applicant = InVerification();
        Assert.False(applicant.ConfirmReregistration(Results)); // not accepted yet
        applicant.Accept(null, Now);

        Assert.False(applicant.Enroll(Guid.NewGuid())); // not confirmed yet
        Assert.True(applicant.ConfirmReregistration(Results));
        Assert.False(applicant.ConfirmReregistration(Results)); // once

        var studentId = Guid.NewGuid();
        Assert.True(applicant.Enroll(studentId));
        Assert.Equal((ApplicantStatus.Enrolled, studentId), (applicant.Status, applicant.StudentId));
        Assert.False(applicant.Enroll(Guid.NewGuid()));
    }
}
