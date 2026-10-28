using AngleSharp.Html.Parser;
using Ganss.Xss;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Content;

/// <summary>
/// Allow-list sanitizer for the rich text staff write (news and announcements). Only the formatting the editor produces
/// survives; scripts, event handlers, styles, images and unsafe link schemes are removed.
/// </summary>
public sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public string Sanitize(string html) => _sanitizer.Sanitize(html);

    public string ToPlainText(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        // Block elements run together without a space once the tags are gone, so add one between paragraphs and list items.
        foreach (var block in document.QuerySelectorAll("p, li, h1, h2, h3, h4, blockquote, br"))
        {
            block.AppendChild(document.CreateTextNode(" "));
        }

        var text = document.Body?.TextContent ?? string.Empty;
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "h2", "h3", "h4", "blockquote", "a", "code", "pre", "hr" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        // A link that opens a new tab must not be able to reach back into this page.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } link)
            {
                link.SetAttribute("rel", "noopener noreferrer nofollow");
                if (link.GetAttribute("target") is not null)
                {
                    link.SetAttribute("target", "_blank");
                }
            }
        };
        return sanitizer;
    }
}
