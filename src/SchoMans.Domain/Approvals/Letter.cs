using System.Globalization;
using SchoMans.Domain.Common;

namespace SchoMans.Domain.Approvals;

/// <summary>
/// The letter of an approved certificate request: an automatic number, a verification code behind the QR code on the
/// PDF, and the stored PDF. Anybody holding the letter can check it with the code.
/// </summary>
public sealed class Letter : BaseEntity
{
    private Letter()
    {
    }

    public Guid RequestId { get; private set; }

    public Request? Request { get; private set; }

    public string LetterNumber { get; private set; } = string.Empty;

    public string VerificationCode { get; private set; } = string.Empty;

    public string PdfKey { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public static Letter Issue(Guid requestId, string letterNumber, string verificationCode, string pdfKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(letterNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfKey);

        return new Letter { RequestId = requestId, LetterNumber = letterNumber, VerificationCode = verificationCode, PdfKey = pdfKey, IssuedAt = now };
    }

    /// <summary>
    /// The number of the sequence-th letter of a year, for example <c>007/SK/SCHOMANS/X/2026</c>: the sequence, the kind of
    /// letter, the school, the month in Roman numerals and the year.
    /// </summary>
    public static string FormatNumber(int sequence, DateOnly issuedOn)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A sequence starts at 1.");
        }

        string[] months = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];
        return string.Create(CultureInfo.InvariantCulture, $"{sequence:000}/SK/SCHOMANS/{months[issuedOn.Month - 1]}/{issuedOn.Year}");
    }
}
