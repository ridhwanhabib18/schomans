using System.Globalization;
using SchoMans.Domain.Common;

namespace SchoMans.Domain.Communication;

public enum GeneralLetterKind
{
    Invitation,
    Notice,
}

/// <summary>
/// A general letter of the school (an invitation or a notice), written by staff with the help of the AI (AI-09). The body is the
/// text staff approved; the number, the letterhead and the signature are the system's, never the AI's. The PDF is stored when the
/// letter is issued; nothing is ever sent automatically.
/// </summary>
public sealed class GeneralLetter : BaseEntity
{
    public const int RecipientMaxLength = 150;
    public const int SubjectMaxLength = 150;
    public const int BodyMaxLength = 4_000;

    private GeneralLetter()
    {
    }

    public GeneralLetterKind Kind { get; private set; }

    public string LetterNumber { get; private set; } = string.Empty;

    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    /// <summary>Paragraphs separated by a blank line.</summary>
    public string Body { get; private set; } = string.Empty;

    public Guid IssuedByUserId { get; private set; }

    public string SignedByName { get; private set; } = string.Empty;

    public string PdfKey { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public static GeneralLetter Issue(
        GeneralLetterKind kind, string letterNumber, string recipient, string subject, string body, Guid issuedByUserId, string signedByName, string pdfKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(letterNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(signedByName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfKey);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(recipient.Length, RecipientMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subject.Length, SubjectMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(body.Length, BodyMaxLength);

        return new GeneralLetter
        {
            Kind = kind,
            LetterNumber = letterNumber,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            IssuedByUserId = issuedByUserId,
            SignedByName = signedByName,
            PdfKey = pdfKey,
            IssuedAt = now,
        };
    }

    /// <summary>
    /// The number of the sequence-th letter of its kind in a year, for example <c>003/UND/SCHOMANS/X/2026</c>: the sequence, the
    /// kind (UND invitation, PEM notice), the school, the month in Roman numerals and the year.
    /// </summary>
    public static string FormatNumber(GeneralLetterKind kind, int sequence, DateOnly issuedOn)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A sequence starts at 1.");
        }

        string[] months = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];
        var code = kind == GeneralLetterKind.Invitation ? "UND" : "PEM";
        return string.Create(CultureInfo.InvariantCulture, $"{sequence:000}/{code}/SCHOMANS/{months[issuedOn.Month - 1]}/{issuedOn.Year}");
    }
}
