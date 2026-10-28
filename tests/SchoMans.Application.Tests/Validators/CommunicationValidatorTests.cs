using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Announcements.UpdateAnnouncement;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Application.Features.News.CreateNews;
using SchoMans.Application.Features.News.ListPublicNews;
using SchoMans.Application.Features.News.UpdateNews;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Tests.Validators;

public class CommunicationValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void A_news_article_needs_a_title_and_a_body()
    {
        var validator = new CreateNewsValidator();

        Assert.True(validator.Validate(new CreateNewsCommand("Open house", "<p>Come</p>", NewsCategory.News, null)).IsValid);
        Assert.False(validator.Validate(new CreateNewsCommand("", "<p>Come</p>", NewsCategory.News, null)).IsValid);
        Assert.False(validator.Validate(new CreateNewsCommand("Open house", "", NewsCategory.News, null)).IsValid);
        Assert.False(validator.Validate(new CreateNewsCommand(new string('x', 201), "<p>Come</p>", NewsCategory.News, null)).IsValid);
        Assert.False(validator.Validate(new CreateNewsCommand("Open house", "<p>Come</p>", (NewsCategory)99, null)).IsValid);
    }

    [Fact]
    public void A_meta_description_is_at_most_300_characters()
    {
        var validator = new CreateNewsValidator();

        Assert.True(validator.Validate(new CreateNewsCommand("T", "<p>x</p>", NewsCategory.News, new string('x', 300))).IsValid);
        Assert.False(validator.Validate(new CreateNewsCommand("T", "<p>x</p>", NewsCategory.News, new string('x', 301))).IsValid);
    }

    [Fact]
    public void A_news_body_has_a_maximum_size()
    {
        Assert.False(new CreateNewsValidator().Validate(new CreateNewsCommand("T", new string('x', 100_001), NewsCategory.News, null)).IsValid);
    }

    [Fact]
    public void Updating_news_needs_the_row_version()
    {
        var update = new UpdateNewsCommand(Id, "T", "<p>x</p>", NewsCategory.News, null, null, "AAAAAAAAB9E=");

        Assert.True(new UpdateNewsValidator().Validate(update).IsValid);
        Assert.False(new UpdateNewsValidator().Validate(update with { RowVersion = "" }).IsValid);
    }

    [Fact]
    public void The_public_news_list_is_paged_like_every_other_list()
    {
        Assert.True(new ListPublicNewsValidator().Validate(new ListPublicNewsQuery(1, 100)).IsValid);
        Assert.False(new ListPublicNewsValidator().Validate(new ListPublicNewsQuery(1, 101)).IsValid);
    }

    [Fact]
    public void An_announcement_needs_a_title_a_body_and_a_sane_audience()
    {
        var validator = new CreateAnnouncementValidator();

        Assert.True(validator.Validate(new CreateAnnouncementCommand("Meeting", "<p>x</p>", false, [UserRole.Teacher], [Id])).IsValid);
        Assert.True(validator.Validate(new CreateAnnouncementCommand("Meeting", "<p>x</p>", true, [], [])).IsValid);
        Assert.False(validator.Validate(new CreateAnnouncementCommand("", "<p>x</p>", false, [], [])).IsValid);
        Assert.False(validator.Validate(new CreateAnnouncementCommand("Meeting", "", false, [], [])).IsValid);
        Assert.False(validator.Validate(new CreateAnnouncementCommand("Meeting", "<p>x</p>", false, [(UserRole)99], [])).IsValid);
        Assert.False(validator.Validate(new CreateAnnouncementCommand("Meeting", "<p>x</p>", false, [], [Guid.Empty])).IsValid);
        Assert.False(validator.Validate(new CreateAnnouncementCommand("Meeting", "<p>x</p>", false, [], Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList())).IsValid);
    }

    [Fact]
    public void Updating_an_announcement_needs_the_row_version()
    {
        var update = new UpdateAnnouncementCommand(Id, "Meeting", "<p>x</p>", false, [], [], null, "AAAAAAAAB9E=");

        Assert.True(new UpdateAnnouncementValidator().Validate(update).IsValid);
        Assert.False(new UpdateAnnouncementValidator().Validate(update with { RowVersion = "x" }).IsValid);
    }

    [Theory]
    [InlineData("news", "image/png", 1024, true)]
    [InlineData("announcements", "image/jpeg", 5 * 1024 * 1024, true)]
    [InlineData("news", "image/webp", 1, true)]
    [InlineData("assignments", "image/png", 1024, false)]
    [InlineData("news", "application/pdf", 1024, false)]
    [InlineData("news", "image/gif", 1024, false)]
    [InlineData("news", "image/png", 0, false)]
    [InlineData("news", "image/png", 5 * 1024 * 1024 + 1, false)]
    public void An_upload_url_is_only_issued_for_a_small_image_of_a_known_module(string module, string type, long size, bool valid)
    {
        var command = new CreateUploadUrlCommand(module, Id, type, size);

        Assert.Equal(valid, new CreateUploadUrlValidator().Validate(command).IsValid);
    }

    [Fact]
    public void An_upload_url_needs_the_entity_id()
    {
        Assert.False(new CreateUploadUrlValidator().Validate(new CreateUploadUrlCommand("news", Guid.Empty, "image/png", 100)).IsValid);
    }
}
