using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;

namespace SchoMans.Application.Features.Requests;

/// <summary>
/// Makes the letter of an approved certificate request: the next number of the year, a random verification code, the QR
/// code of the address where the letter can be checked, and the PDF in private storage. It adds the letter to the
/// context; the caller saves it together with the decision.
/// </summary>
public sealed class IssueLetterHandler(IAppDbContext db, ILetterPdf pdf, IQrCodeGenerator qr, ISiteLinks links, IFileStorage storage, ISchoolClock clock)
{
    public static string PdfKey(Guid requestId) => $"letters/{requestId:N}.pdf";

    /// <param name="attempt">0 for the next number; a retry after a clash on the number asks for the one after.</param>
    public async Task<Letter> IssueAsync(Request request, string signedBy, int attempt, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = clock.ToSchoolDate(now);
        var yearStart = clock.ToUtc(new DateOnly(today.Year, 1, 1), TimeOnly.MinValue);
        var yearEnd = clock.ToUtc(new DateOnly(today.Year + 1, 1, 1), TimeOnly.MinValue);
        var sequence = await db.Letters.CountAsync(l => l.IssuedAt >= yearStart && l.IssuedAt < yearEnd, ct) + 1 + attempt;

        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new { s.User!.FullName, s.Nisn, ClassroomName = s.Classroom!.Name, YearName = s.Classroom.AcademicYear!.Name })
            .FirstAsync(ct);

        var number = Letter.FormatNumber(sequence, today);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
        var url = links.PublicUrl($"/verify/{code}");

        var document = new LetterDocument(number, student.FullName, student.Nisn, student.ClassroomName, student.YearName, request.Reason, today, signedBy, url, qr.Png(url));
        var key = PdfKey(request.Id);
        await storage.UploadAsync(FileBucket.Private, key, pdf.Render(document), "application/pdf", ct);

        var letter = Letter.Issue(request.Id, number, code, key, now);
        db.Letters.Add(letter);
        return letter;
    }
}
