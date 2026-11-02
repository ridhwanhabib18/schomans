using SchoMans.Domain.Approvals;

namespace SchoMans.Domain.Tests.Approvals;

public class ApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Approver = Guid.NewGuid();

    private static Request Leave() =>
        Request.Create(RequestType.StudentLeave, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13), " Fever ");

    // ---- creating ----

    [Fact]
    public void A_leave_needs_a_student_and_days_in_order()
    {
        var request = Leave();

        Assert.Equal((RequestStatus.Pending, 1, "Fever"), (request.Status, request.CurrentStep, request.Reason));
        Assert.Throws<ArgumentException>(() => Request.Create(RequestType.StudentLeave, Guid.NewGuid(), null, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13), "x"));
        Assert.Throws<ArgumentException>(() => Request.Create(RequestType.StudentLeave, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 12), "x"));
        Assert.Throws<ArgumentException>(() => Request.Create(RequestType.StudentLeave, Guid.NewGuid(), Guid.NewGuid(), null, null, "x"));
        Assert.Throws<ArgumentException>(() => Request.Create(RequestType.StudentLeave, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13), " "));
    }

    [Fact]
    public void A_teacher_s_leave_is_not_about_a_student_and_a_letter_has_no_days()
    {
        Assert.Throws<ArgumentException>(() => Request.Create(RequestType.TeacherLeave, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12), "x"));
        Assert.Null(Request.Create(RequestType.TeacherLeave, Guid.NewGuid(), null, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12), "x").StudentId);

        var letter = Request.Create(RequestType.CertificateLetter, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13), "For a scholarship");
        Assert.Null(letter.StartDate);
        Assert.Null(letter.EndDate);
    }

    // ---- deciding ----

    [Fact]
    public void An_approval_moves_the_request_to_the_next_step_and_the_last_one_approves_it()
    {
        var request = Leave();

        Assert.True(request.Decide(ApprovalDecision.Approved, Approver, "Get well", totalSteps: 2, Now));
        Assert.Equal((RequestStatus.Pending, 2), (request.Status, request.CurrentStep));
        Assert.Null(request.DecidedAt);

        Assert.True(request.Decide(ApprovalDecision.Approved, Approver, null, totalSteps: 2, Now.AddHours(1)));
        Assert.Equal((RequestStatus.Approved, Now.AddHours(1)), (request.Status, request.DecidedAt));
        Assert.Equal([1, 2], request.Approvals.Select(a => a.Step));
        Assert.Equal("Get well", request.Approvals[0].Notes);
        Assert.Null(request.Approvals[1].Notes);
    }

    [Fact]
    public void A_rejection_ends_the_request_at_once_and_needs_a_note()
    {
        var request = Leave();
        Assert.Throws<ArgumentException>(() => request.Decide(ApprovalDecision.Rejected, Approver, " ", totalSteps: 2, Now));
        Assert.Empty(request.Approvals);

        Assert.True(request.Decide(ApprovalDecision.Rejected, Approver, "No days left", totalSteps: 2, Now));

        Assert.Equal((RequestStatus.Rejected, 1), (request.Status, request.CurrentStep));
        Assert.Equal(ApprovalDecision.Rejected, Assert.Single(request.Approvals).Decision);
    }

    [Fact]
    public void A_request_that_is_not_pending_takes_no_more_decisions()
    {
        var request = Leave();
        request.Decide(ApprovalDecision.Rejected, Approver, "No", totalSteps: 1, Now);

        Assert.False(request.Decide(ApprovalDecision.Approved, Approver, null, totalSteps: 1, Now));
        Assert.False(request.Cancel(Now));
        Assert.Single(request.Approvals);
    }

    [Fact]
    public void The_requester_can_withdraw_a_pending_request_only()
    {
        var request = Leave();

        Assert.True(request.Cancel(Now));
        Assert.Equal((RequestStatus.Cancelled, Now), (request.Status, request.DecidedAt));
        Assert.False(request.Decide(ApprovalDecision.Approved, Approver, null, totalSteps: 1, Now));
    }

    [Fact]
    public void A_note_is_not_longer_than_the_limit_and_a_chain_needs_a_step()
    {
        Assert.Throws<ArgumentException>(() => Leave().Decide(ApprovalDecision.Approved, Approver, new string('x', Request.NoteMaxLength + 1), 1, Now));
        Assert.False(Leave().Decide(ApprovalDecision.Approved, Approver, null, totalSteps: 0, Now));
    }

    // ---- chains ----

    [Fact]
    public void A_chain_has_one_to_five_steps_and_a_teacher_s_leave_has_no_homeroom_step()
    {
        Assert.True(ApprovalChainStep.IsValidChain(RequestType.StudentLeave, [ApproverRole.HomeroomTeacher, ApproverRole.Principal]));
        Assert.True(ApprovalChainStep.IsValidChain(RequestType.TeacherLeave, [ApproverRole.Principal]));
        Assert.False(ApprovalChainStep.IsValidChain(RequestType.TeacherLeave, [ApproverRole.HomeroomTeacher, ApproverRole.Principal]));
        Assert.False(ApprovalChainStep.IsValidChain(RequestType.StudentLeave, []));
        Assert.False(ApprovalChainStep.IsValidChain(RequestType.StudentLeave, Enumerable.Repeat(ApproverRole.Admin, 6).ToList()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApprovalChainStep.Create(RequestType.StudentLeave, 0, ApproverRole.Admin));
    }

    // ---- letters ----

    [Theory]
    [InlineData(1, 10, 2026, "001/SK/SCHOMANS/X/2026")]
    [InlineData(7, 1, 2027, "007/SK/SCHOMANS/I/2027")]
    [InlineData(123, 12, 2026, "123/SK/SCHOMANS/XII/2026")]
    [InlineData(1234, 4, 2026, "1234/SK/SCHOMANS/IV/2026")]
    public void The_letter_number_has_the_sequence_the_month_in_roman_numerals_and_the_year(int sequence, int month, int year, string expected)
    {
        Assert.Equal(expected, Letter.FormatNumber(sequence, new DateOnly(year, month, 15)));
    }

    [Fact]
    public void A_letter_needs_a_number_a_code_and_a_pdf()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Letter.FormatNumber(0, new DateOnly(2026, 1, 1)));
        Assert.Throws<ArgumentException>(() => Letter.Issue(Guid.NewGuid(), " ", "code", "key", Now));
        Assert.Throws<ArgumentException>(() => Letter.Issue(Guid.NewGuid(), "001/SK", "", "key", Now));
        Assert.Throws<ArgumentException>(() => Letter.Issue(Guid.NewGuid(), "001/SK", "code", " ", Now));

        var letter = Letter.Issue(Guid.NewGuid(), "001/SK/SCHOMANS/X/2026", "abc123", "letters/x.pdf", Now);
        Assert.Equal(("001/SK/SCHOMANS/X/2026", "abc123", Now), (letter.LetterNumber, letter.VerificationCode, letter.IssuedAt));
    }
}
