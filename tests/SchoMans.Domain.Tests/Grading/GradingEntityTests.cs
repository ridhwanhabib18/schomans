using SchoMans.Domain.Grading;

namespace SchoMans.Domain.Tests.Grading;

public class GradingEntityTests
{
    private static List<GradeBand> Scale() =>
        [GradeBand.Create("A", 90), GradeBand.Create("B", 80), GradeBand.Create("C", 70), GradeBand.Create("D", 60), GradeBand.Create("E", 0)];

    [Theory]
    [InlineData(100, "A")]
    [InlineData(90, "A")]
    [InlineData(89.99, "B")]
    [InlineData(70, "C")]
    [InlineData(60, "D")]
    [InlineData(59.99, "E")]
    [InlineData(0, "E")]
    public void A_grade_gets_the_band_with_the_highest_minimum_it_reaches(double score, string label)
    {
        Assert.Equal(label, GradeBand.LabelFor((decimal)score, Scale()));
    }

    [Fact]
    public void Bands_that_do_not_cover_a_grade_give_no_label()
    {
        Assert.Null(GradeBand.LabelFor(10, [GradeBand.Create("A", 90)]));
    }

    [Fact]
    public void A_band_needs_a_short_label_and_a_minimum_between_0_and_100()
    {
        Assert.Throws<ArgumentException>(() => GradeBand.Create(" ", 10));
        Assert.Throws<ArgumentException>(() => GradeBand.Create(new string('x', GradeBand.LabelMaxLength + 1), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => GradeBand.Create("A", 101));
        Assert.Equal("A", GradeBand.Create(" A ", 90).Label);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void A_score_stays_between_0_and_100(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Grade.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GradeComponent.Quiz, (decimal)score));
        Assert.Throws<ArgumentOutOfRangeException>(() => GradeWeight.Create(Guid.NewGuid(), GradeComponent.Quiz, (decimal)score));
    }

    [Fact]
    public void A_grade_can_be_corrected_and_remembers_the_submission_it_came_from()
    {
        var submission = Guid.NewGuid();
        var grade = Grade.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GradeComponent.Assignment, 70, submission);

        grade.SetScore(85.5m);

        Assert.Equal(85.5m, grade.Score);
        Assert.Equal(submission, grade.SubmissionId);
    }

    [Fact]
    public void Editing_an_achievement_description_makes_it_a_draft_again_until_it_is_accepted()
    {
        var description = AchievementDescription.Create(Guid.NewGuid(), Guid.NewGuid(), "  Masters fractions.  ");
        description.Accept();
        Assert.Equal(AchievementStatus.Accepted, description.Status);

        description.Write("Masters fractions and decimals.");

        Assert.Equal(AchievementStatus.Draft, description.Status);
        Assert.Equal("Masters fractions and decimals.", description.Body);
        Assert.False(description.IsAiDrafted);
    }

    [Fact]
    public void An_achievement_description_is_not_empty_and_not_too_long()
    {
        Assert.Throws<ArgumentException>(() => AchievementDescription.Create(Guid.NewGuid(), Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => AchievementDescription.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', AchievementDescription.BodyMaxLength + 1)));
    }

    [Fact]
    public void A_report_card_starts_as_a_draft_and_is_published_with_its_pdf()
    {
        var card = ReportCard.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(ReportCardStatus.Draft, card.Status);
        Assert.Null(card.PdfKey);

        var now = new DateTimeOffset(2026, 12, 18, 3, 0, 0, TimeSpan.Zero);
        card.AttachPdf("report-cards/x.pdf", now);
        card.Publish();

        Assert.Equal(ReportCardStatus.Published, card.Status);
        Assert.Equal("report-cards/x.pdf", card.PdfKey);
        Assert.Equal(now, card.GeneratedAt);

        card.Unpublish();
        Assert.Equal(ReportCardStatus.Draft, card.Status);
        Assert.Throws<ArgumentException>(() => card.AttachPdf(" ", now));
    }
}
