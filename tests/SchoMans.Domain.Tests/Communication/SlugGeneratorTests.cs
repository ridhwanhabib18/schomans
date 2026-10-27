using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Open House 2027", "open-house-2027")]
    [InlineData("  Welcome   to   the New Year!  ", "welcome-to-the-new-year")]
    [InlineData("Our students won: 1st place (regional olympiad)", "our-students-won-1st-place-regional-olympiad")]
    [InlineData("Café Müller — Pengumuman", "cafe-muller-pengumuman")]
    [InlineData("Already-a-slug", "already-a-slug")]
    public void Turns_a_title_into_lower_case_words_joined_by_dashes(string title, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Slugify(title));
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("日本語")]
    public void A_title_without_usable_characters_still_gets_an_address(string title)
    {
        Assert.Equal("article", SlugGenerator.Slugify(title));
    }

    [Fact]
    public void Long_titles_are_cut_without_a_trailing_dash()
    {
        var slug = SlugGenerator.Slugify(string.Join(' ', Enumerable.Repeat("announcement", 20)));

        Assert.True(slug.Length <= SlugGenerator.MaxLength);
        Assert.False(slug.EndsWith('-'));
        Assert.StartsWith("announcement-announcement", slug, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_blank_title()
    {
        Assert.ThrowsAny<ArgumentException>(() => SlugGenerator.Slugify("  "));
    }

    [Theory]
    [InlineData(1, "open-house")]
    [InlineData(2, "open-house-2")]
    [InlineData(3, "open-house-3")]
    public void Adds_a_number_for_the_second_and_later_article_with_the_same_title(int number, string expected)
    {
        Assert.Equal(expected, SlugGenerator.WithSuffix("open-house", number));
    }
}
