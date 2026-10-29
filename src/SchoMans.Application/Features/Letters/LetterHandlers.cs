using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Requests;

namespace SchoMans.Application.Features.Letters;

public sealed record GetLetterUrlQuery(Guid RequestId);

public sealed record VerifyLetterQuery(string Code);

public sealed record LetterUrlDto(string Url, DateTimeOffset ExpiresAt, string LetterNumber);

/// <summary>What anybody holding the letter may learn from its code: that it is real, its number, who it is about and when it was issued.</summary>
public sealed record LetterVerificationDto(bool Valid, string? LetterNumber, string? StudentName, DateOnly? IssuedOn);

/// <summary>A short-lived download address for the PDF, for everybody who may see the request.</summary>
public sealed class GetLetterUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LetterUrlDto>> Handle(GetLetterUrlQuery query, CancellationToken ct)
    {
        var letter = await db.Letters.AsNoTracking().Include(l => l.Request).FirstOrDefaultAsync(l => l.RequestId == query.RequestId, ct);
        if (letter is null)
        {
            return Error.NotFound("letters.not_found", "There is no letter for this request.");
        }

        if (!await RequestSupport.CanViewAsync(db, currentUser, letter.Request!, ct))
        {
            return Error.Forbidden("requests.not_yours", "You may not open this letter.");
        }

        return new LetterUrlDto(storage.CreateDownloadUrl(letter.PdfKey, Lifetime), timeProvider.GetUtcNow() + Lifetime, letter.LetterNumber);
    }
}

/// <summary>Public: the authenticity check behind the QR code. An unknown code answers "not valid", not an error.</summary>
public sealed class VerifyLetterHandler(IAppDbContext db, ISchoolClock clock)
{
    public async Task<Result<LetterVerificationDto>> Handle(VerifyLetterQuery query, CancellationToken ct)
    {
        var code = (query.Code ?? string.Empty).Trim().ToLowerInvariant();
        var letter = code.Length is >= 8 and <= 40
            ? await db.Letters.AsNoTracking()
                .Where(l => l.VerificationCode == code)
                .Select(l => new { l.LetterNumber, l.IssuedAt, StudentName = l.Request!.Student!.User!.FullName })
                .FirstOrDefaultAsync(ct)
            : null;

        return letter is null
            ? new LetterVerificationDto(false, null, null, null)
            : new LetterVerificationDto(true, letter.LetterNumber, letter.StudentName, clock.ToSchoolDate(letter.IssuedAt));
    }
}
