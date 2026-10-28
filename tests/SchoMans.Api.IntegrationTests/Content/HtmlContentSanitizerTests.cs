using SchoMans.Infrastructure.Content;

namespace SchoMans.Api.IntegrationTests.Content;

public class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sanitizer = new();

    [Theory]
    [InlineData("<p>Plain paragraph</p>")]
    [InlineData("<p>Some <strong>bold</strong>, <em>italic</em>, <u>underlined</u> and <s>struck</s> text</p>")]
    [InlineData("<h2>Heading</h2><h3>Sub</h3><h4>Smaller</h4>")]
    [InlineData("<ul><li>one</li><li>two</li></ul><ol><li>first</li></ol>")]
    [InlineData("<blockquote>A quote</blockquote>")]
    [InlineData("<pre><code>var x = 1;</code></pre>")]
    [InlineData("<p>line<br>break</p><hr>")]
    public void The_formatting_the_editor_produces_is_kept(string html)
    {
        Assert.Equal(html, _sanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>x</p>", "<p>x</p>")]
    [InlineData("<p onclick=\"steal()\">x</p>", "<p>x</p>")]
    [InlineData("<p style=\"position:fixed;top:0\">x</p>", "<p>x</p>")]
    [InlineData("<p class=\"big\" id=\"a\" data-x=\"1\">x</p>", "<p>x</p>")]
    [InlineData("<img src=x onerror=alert(1)><p>x</p>", "<p>x</p>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe><p>x</p>", "<p>x</p>")]
    [InlineData("<object data=\"x\"></object><embed src=\"x\"><p>x</p>", "<p>x</p>")]
    [InlineData("<form action=\"https://evil.example\"><input name=p></form><p>x</p>", "<p>x</p>")]
    [InlineData("<style>body{display:none}</style><p>x</p>", "<p>x</p>")]
    [InlineData("<svg onload=alert(1)></svg><p>x</p>", "<p>x</p>")]
    [InlineData("<math><mi>x</mi></math><p>x</p>", "<p>x</p>")]
    [InlineData("<!-- a comment --><p>x</p>", "<p>x</p>")]
    [InlineData("<p>x</p><meta http-equiv=\"refresh\" content=\"0;url=https://evil.example\">", "<p>x</p>")]
    public void Anything_that_can_run_code_or_change_the_page_is_removed(string html, string expected)
    {
        Assert.Equal(expected, _sanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("vbscript:msgbox(1)")]
    public void Links_with_an_unsafe_scheme_lose_their_address(string href)
    {
        var result = _sanitizer.Sanitize($"<a href=\"{href}\">click</a>");

        Assert.DoesNotContain("javascript", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("click", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    [InlineData("mailto:office@school.example")]
    public void Links_with_a_safe_scheme_are_kept(string href)
    {
        Assert.Contains($"href=\"{href}\"", _sanitizer.Sanitize($"<a href=\"{href}\">link</a>"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_link_cannot_reach_back_into_the_page_that_opened_it()
    {
        var result = _sanitizer.Sanitize("<a href=\"https://example.com\" target=\"_blank\" rel=\"opener\">x</a><a href=\"https://example.org\">y</a>");

        Assert.Equal(2, result.Split("noopener noreferrer nofollow").Length - 1);
        Assert.DoesNotContain("rel=\"opener\"", result, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Broken_markup_is_repaired_not_passed_through()
    {
        var result = _sanitizer.Sanitize("<p>unclosed <strong>bold<script>alert(1)");

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<strong>bold</strong>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_is_never_double_encoded_and_special_characters_stay_safe()
    {
        var result = _sanitizer.Sanitize("<p>5 &lt; 6 &amp; 7 &gt; 3 &quot;quoted&quot;</p>");

        Assert.Equal("<p>5 &lt; 6 &amp; 7 &gt; 3 \"quoted\"</p>", result);
    }

    [Fact]
    public void Sanitizing_twice_changes_nothing()
    {
        const string evil = "<p onclick=\"x()\">a<script>b</script><a href=\"javascript:c\">d</a></p>";

        var once = _sanitizer.Sanitize(evil);

        Assert.Equal(once, _sanitizer.Sanitize(once));
    }

    // ---- plain text for excerpts and meta descriptions ----

    [Fact]
    public void Plain_text_separates_paragraphs_and_list_items_with_a_space()
    {
        var text = _sanitizer.ToPlainText("<p>First paragraph.</p><p>Second.</p><ul><li>one</li><li>two</li></ul><h2>Title</h2>");

        Assert.Equal("First paragraph. Second. one two Title", text);
    }

    [Fact]
    public void Plain_text_decodes_entities_collapses_whitespace_and_drops_tags()
    {
        var text = _sanitizer.ToPlainText("<p>Fish   &amp;\n  chips <strong>today</strong><br>only</p>");

        Assert.Equal("Fish & chips today only", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    public void Plain_text_of_nothing_is_empty(string html)
    {
        Assert.Equal(string.Empty, _sanitizer.ToPlainText(html));
    }
}
