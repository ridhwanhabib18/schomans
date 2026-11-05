using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class GeneralLetterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(GeneralLetterKind.Invitation, 1, 10, 2026, "001/UND/SCHOMANS/X/2026")]
    [InlineData(GeneralLetterKind.Notice, 7, 1, 2027, "007/PEM/SCHOMANS/I/2027")]
    [InlineData(GeneralLetterKind.Notice, 1234, 12, 2026, "1234/PEM/SCHOMANS/XII/2026")]
    public void The_number_has_the_sequence_the_kind_the_month_in_roman_numerals_and_the_year(GeneralLetterKind kind, int sequence, int month, int year, string expected)
    {
        Assert.Equal(expected, GeneralLetter.FormatNumber(kind, sequence, new DateOnly(year, month, 15)));
    }

    [Fact]
    public void A_letter_needs_a_number_a_recipient_a_subject_a_body_a_signer_and_a_pdf_within_the_limits()
    {
        var by = Guid.NewGuid();
        GeneralLetter Make(string number = "001/UND/SCHOMANS/X/2026", string recipient = "Parents", string subject = "Open day", string body = "Text", string signer = "Sari", string key = "k.pdf") =>
            GeneralLetter.Issue(GeneralLetterKind.Invitation, number, recipient, subject, body, by, signer, key, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => GeneralLetter.FormatNumber(GeneralLetterKind.Notice, 0, new DateOnly(2026, 1, 1)));
        Assert.Throws<ArgumentException>(() => Make(number: " "));
        Assert.Throws<ArgumentException>(() => Make(recipient: ""));
        Assert.Throws<ArgumentException>(() => Make(subject: " "));
        Assert.Throws<ArgumentException>(() => Make(body: ""));
        Assert.Throws<ArgumentException>(() => Make(signer: ""));
        Assert.Throws<ArgumentException>(() => Make(key: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(recipient: new string('r', GeneralLetter.RecipientMaxLength + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(subject: new string('s', GeneralLetter.SubjectMaxLength + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(body: new string('b', GeneralLetter.BodyMaxLength + 1)));

        var letter = Make();
        Assert.Equal(("001/UND/SCHOMANS/X/2026", "Parents", "Open day", "Text", "Sari", Now, by), (letter.LetterNumber, letter.Recipient, letter.Subject, letter.Body, letter.SignedByName, letter.IssuedAt, letter.IssuedByUserId));
    }
}
