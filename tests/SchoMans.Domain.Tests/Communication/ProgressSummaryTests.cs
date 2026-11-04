using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class ProgressSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();

    private static ProgressSummary Draft() => ProgressSummary.Draft(Student, "2026-10", "A steady month.", isAiDrafted: true);

    [Fact]
    public void A_new_summary_is_an_ai_draft_that_has_not_been_approved_or_sent()
    {
        var summary = Draft();

        Assert.Equal(ProgressSummaryStatus.Draft, summary.Status);
        Assert.True(summary.IsAiDrafted);
        Assert.Null(summary.ApprovedById);
        Assert.Null(summary.SentAt);
    }

    [Theory]
    [InlineData("2026-1")]
    [InlineData("2026-13")]
    [InlineData("october")]
    [InlineData("")]
    public void A_month_must_look_like_yyyy_MM(string month)
    {
        Assert.False(ProgressSummary.IsMonthKey(month));
        Assert.Throws<ArgumentException>(() => ProgressSummary.Draft(Student, month, "Text", true));
    }

    [Fact]
    public void The_month_key_is_year_and_month_with_a_leading_zero()
    {
        Assert.Equal("2026-03", ProgressSummary.MonthKey(new DateOnly(2026, 3, 15)));
        Assert.True(ProgressSummary.IsMonthKey("2026-03"));
    }

    [Fact]
    public void A_summary_goes_from_draft_to_approved_to_sent()
    {
        var summary = Draft();

        Assert.True(summary.Approve(Teacher));
        Assert.Equal(ProgressSummaryStatus.Approved, summary.Status);
        Assert.Equal(Teacher, summary.ApprovedById);

        Assert.True(summary.Send(Now));
        Assert.Equal(ProgressSummaryStatus.Sent, summary.Status);
        Assert.Equal(Now, summary.SentAt);
    }

    [Fact]
    public void A_draft_cannot_be_sent_before_it_is_approved()
    {
        var summary = Draft();

        Assert.False(summary.Send(Now));
        Assert.Equal(ProgressSummaryStatus.Draft, summary.Status);
    }

    [Fact]
    public void Approving_twice_or_after_sending_changes_nothing()
    {
        var summary = Draft();
        summary.Approve(Teacher);

        Assert.False(summary.Approve(Guid.NewGuid()));
        Assert.Equal(Teacher, summary.ApprovedById);

        summary.Send(Now);
        Assert.False(summary.Approve(Teacher));
        Assert.False(summary.Send(Now.AddDays(1)));
        Assert.Equal(Now, summary.SentAt);
    }

    [Fact]
    public void An_edit_makes_it_the_teachers_own_text_and_asks_for_a_new_approval()
    {
        var summary = Draft();
        summary.Approve(Teacher);

        Assert.True(summary.Edit("  My own words.  "));

        Assert.Equal("My own words.", summary.Body);
        Assert.False(summary.IsAiDrafted);
        Assert.Equal(ProgressSummaryStatus.Draft, summary.Status);
        Assert.Null(summary.ApprovedById);
    }

    [Fact]
    public void A_sent_summary_can_no_longer_change()
    {
        var summary = Draft();
        summary.Approve(Teacher);
        summary.Send(Now);

        Assert.False(summary.Edit("Changed after sending."));
        Assert.False(summary.Redraft("A new AI text."));
        Assert.Equal("A steady month.", summary.Body);
    }

    [Fact]
    public void A_new_ai_draft_replaces_the_text_and_clears_the_approval()
    {
        var summary = Draft();
        summary.Edit("Teacher text.");
        summary.Approve(Teacher);

        Assert.True(summary.Redraft("Fresh AI text."));

        Assert.Equal("Fresh AI text.", summary.Body);
        Assert.True(summary.IsAiDrafted);
        Assert.Equal(ProgressSummaryStatus.Draft, summary.Status);
        Assert.Null(summary.ApprovedById);
    }

    [Fact]
    public void The_text_cannot_be_empty_or_longer_than_the_limit()
    {
        var summary = Draft();

        Assert.Throws<ArgumentException>(() => summary.Edit("   "));
        Assert.Throws<ArgumentException>(() => summary.Edit(new string('x', ProgressSummary.BodyMaxLength + 1)));
    }
}
