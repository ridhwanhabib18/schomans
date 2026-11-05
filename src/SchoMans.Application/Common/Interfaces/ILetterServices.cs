namespace SchoMans.Application.Common.Interfaces;

/// <summary>Everything printed on a certificate letter. The letterhead is the renderer's business.</summary>
public sealed record LetterDocument(
    string LetterNumber,
    string StudentName,
    string Nisn,
    string ClassroomName,
    string AcademicYearName,
    string Purpose,
    DateOnly IssuedOn,
    string SignedByName,
    string VerificationUrl,
    byte[] QrPng);

/// <summary>Turns a certificate letter into a print-ready PDF (QuestPDF behind the interface).</summary>
public interface ILetterPdf
{
    byte[] Render(LetterDocument document);
}

/// <summary>Everything printed on a general letter (AI-09): the approved text and the facts the system adds. The letterhead is the renderer's business.</summary>
public sealed record GeneralLetterDocument(
    string LetterNumber,
    string Heading,
    string Recipient,
    string Subject,
    IReadOnlyList<string> Paragraphs,
    DateOnly IssuedOn,
    string SignedByName);

/// <summary>Turns a general letter into a print-ready PDF (QuestPDF behind the interface).</summary>
public interface IGeneralLetterPdf
{
    byte[] Render(GeneralLetterDocument document);
}

/// <summary>Draws a QR code as a PNG image.</summary>
public interface IQrCodeGenerator
{
    byte[] Png(string content);
}

/// <summary>Addresses of the public web app, for links that leave the system (the QR code of a letter).</summary>
public interface ISiteLinks
{
    /// <summary>The full address of a path of the web app, for example <c>/verify/abc</c>.</summary>
    string PublicUrl(string path);
}
