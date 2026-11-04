using SchoMans.Application.Common.Errors;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>The text of a PDF and how many pages it has.</summary>
public sealed record PdfText(int Pages, string Text);

/// <summary>Reads the text out of a PDF (PdfPig behind the interface). Only the text is used: images and layout are ignored.</summary>
public interface IPdfTextReader
{
    /// <summary>
    /// The text of the PDF, or why it cannot be used: not a readable PDF (<c>questions.pdf_invalid</c>), more than <paramref name="maxPages"/>
    /// pages (<c>questions.pdf_too_long</c>), or no text in it, as in a scan (<c>questions.pdf_no_text</c>).
    /// </summary>
    Result<PdfText> Read(Stream pdf, int maxPages);
}
