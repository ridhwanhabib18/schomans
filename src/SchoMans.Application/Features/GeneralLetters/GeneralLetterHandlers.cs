using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Drafts;
using SchoMans.Application.Features.Letters;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.GeneralLetters;

public sealed record GeneralLetterDto(Guid Id, GeneralLetterKind Kind, string LetterNumber, string Recipient, string Subject, string SignedByName, DateTimeOffset IssuedAt);

// ---- issue ----

/// <param name="Paragraphs">The text staff approved, usually the AI draft after their edits. The system adds letterhead, number and signature.</param>
public sealed record IssueGeneralLetterCommand(GeneralLetterKind Kind, string Recipient, string Subject, List<string> Paragraphs);

public sealed class IssueGeneralLetterValidator : AbstractValidator<IssueGeneralLetterCommand>
{
    public IssueGeneralLetterValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(GeneralLetter.RecipientMaxLength);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(GeneralLetter.SubjectMaxLength);
        RuleFor(x => x.Paragraphs).Cascade(CascadeMode.Stop).NotNull().Must(p => p is { Count: >= 1 and <= DraftPrompt.MaxParagraphs })
            .Must(p => p.Sum(x => x?.Trim().Length ?? 0) + (2 * Math.Max(0, p.Count - 1)) <= GeneralLetter.BodyMaxLength)
            .WithMessage("The letter is too long.");
        RuleForEach(x => x.Paragraphs).Must(p => !string.IsNullOrWhiteSpace(p) && p.Trim().Length <= DraftPrompt.ParagraphMaxLength);
    }
}

/// <summary>
/// Staff issue a general letter: the next number of its kind in the year, the letterhead and the signature of the signed-in person
/// around the approved text, and the PDF in private storage. Only staff who may edit announcements can do this; nothing is sent to anybody.
/// </summary>
public sealed class IssueGeneralLetterHandler(IAppDbContext db, ICurrentUser currentUser, IGeneralLetterPdf pdf, IFileStorage storage, ISchoolClock clock)
{
    private const int Attempts = 3;

    public async Task<Result<GeneralLetterDto>> Handle(IssueGeneralLetterCommand command, CancellationToken ct)
    {
        var paragraphs = command.Paragraphs.Select(p => p.Trim()).ToList();
        var recipient = command.Recipient.Trim();
        var subject = command.Subject.Trim();
        var now = clock.UtcNow;
        var today = clock.ToSchoolDate(now);
        var yearStart = clock.ToUtc(new DateOnly(today.Year, 1, 1), TimeOnly.MinValue);
        var yearEnd = clock.ToUtc(new DateOnly(today.Year + 1, 1, 1), TimeOnly.MinValue);

        // The number is the next one of the year; two letters issued at once can clash on it, so it is tried again.
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var sequence = await db.GeneralLetters.CountAsync(l => l.Kind == command.Kind && l.IssuedAt >= yearStart && l.IssuedAt < yearEnd, ct) + 1 + attempt;
            var number = GeneralLetter.FormatNumber(command.Kind, sequence, today);
            var key = $"general-letters/{Guid.NewGuid():N}.pdf";
            var heading = command.Kind == GeneralLetterKind.Invitation ? "Invitation" : "Notice";

            var bytes = pdf.Render(new GeneralLetterDocument(number, heading, recipient, subject, paragraphs, today, currentUser.FullName));
            await storage.UploadAsync(FileBucket.Private, key, bytes, "application/pdf", ct);

            var letter = GeneralLetter.Issue(command.Kind, number, recipient, subject, string.Join("\n\n", paragraphs), currentUser.UserId, currentUser.FullName, key, now);
            db.GeneralLetters.Add(letter);
            try
            {
                await db.SaveChangesAsync(ct);
                return ToDto(letter);
            }
            catch (DbUpdateException) when (attempt < Attempts - 1)
            {
                foreach (var entry in db.ChangeTracker.Entries<GeneralLetter>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
            catch (DbUpdateException)
            {
                break;
            }
        }

        return Error.Conflict("general_letters.number_conflict", "The letter could not be numbered. Try again.");
    }

    internal static GeneralLetterDto ToDto(GeneralLetter l) => new(l.Id, l.Kind, l.LetterNumber, l.Recipient, l.Subject, l.SignedByName, l.IssuedAt);
}

// ---- list ----

public sealed record ListGeneralLettersQuery(int Page = 1, int PageSize = 20);

public sealed class ListGeneralLettersValidator : AbstractValidator<ListGeneralLettersQuery>
{
    public ListGeneralLettersValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}

public sealed class ListGeneralLettersHandler(IAppDbContext db)
{
    public async Task<Result<PagedResult<GeneralLetterDto>>> Handle(ListGeneralLettersQuery query, CancellationToken ct) =>
        await db.GeneralLetters.AsNoTracking()
            .OrderByDescending(l => l.IssuedAt).ThenByDescending(l => l.Id)
            .Select(l => new GeneralLetterDto(l.Id, l.Kind, l.LetterNumber, l.Recipient, l.Subject, l.SignedByName, l.IssuedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
}

// ---- download ----

public sealed record GetGeneralLetterUrlQuery(Guid Id);

/// <summary>A short-lived download address for the PDF of a general letter.</summary>
public sealed class GetGeneralLetterUrlHandler(IAppDbContext db, IFileStorage storage, TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LetterUrlDto>> Handle(GetGeneralLetterUrlQuery query, CancellationToken ct)
    {
        var letter = await db.GeneralLetters.AsNoTracking().FirstOrDefaultAsync(l => l.Id == query.Id, ct);
        return letter is null
            ? Error.NotFound("general_letters.not_found", "The letter does not exist.")
            : new LetterUrlDto(storage.CreateDownloadUrl(letter.PdfKey, Lifetime), timeProvider.GetUtcNow() + Lifetime, letter.LetterNumber);
    }
}
