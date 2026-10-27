namespace SchoMans.Application.Common.Interfaces;

/// <summary>Removes everything dangerous (scripts, event handlers, unsafe links) from rich-text HTML written by staff, before it is stored.</summary>
public interface IHtmlContentSanitizer
{
    string Sanitize(string html);

    /// <summary>The text of the HTML without tags, for excerpts and meta descriptions.</summary>
    string ToPlainText(string html);
}
