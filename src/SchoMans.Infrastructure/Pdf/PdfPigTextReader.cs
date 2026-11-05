using System.Text;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>Reads the text of a PDF with PdfPig (Apache-2.0). It reads the text layer only; a scanned PDF has none and is refused.</summary>
public sealed class PdfPigTextReader : IPdfTextReader
{
    public Result<PdfText> Read(Stream pdf, int maxPages)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages > maxPages)
            {
                return Error.Validation("questions.pdf_too_long", $"The PDF has {document.NumberOfPages} pages; at most {maxPages} are allowed.");
            }

            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                text.AppendLine(page.Text);
            }

            var all = text.ToString().Trim();
            return all.Length == 0
                ? Error.Validation("questions.pdf_no_text", "The PDF has no text to read; a scanned document cannot be used.")
                : new PdfText(document.NumberOfPages, all);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // PdfPig throws its own exceptions for damaged, encrypted and malformed files; none of them is the caller's to handle.
            return Error.Validation("questions.pdf_invalid", "The file is not a PDF that can be read.");
        }
    }
}
