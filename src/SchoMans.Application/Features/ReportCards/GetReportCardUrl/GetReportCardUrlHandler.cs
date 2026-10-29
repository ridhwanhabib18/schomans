using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.ReportCards.GetReportCardUrl;

public sealed record GetReportCardUrlQuery(Guid StudentId, Guid TermId);

/// <summary>
/// A short-lived download address for the PDF. The student and their parents get it only while the report card is
/// published; the homeroom teacher of the student and the principal can always open it.
/// </summary>
public sealed class GetReportCardUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<ReportCardUrlDto>> Handle(GetReportCardUrlQuery query, CancellationToken ct)
    {
        var reportCard = await db.ReportCards.AsNoTracking()
            .Where(r => r.StudentId == query.StudentId && r.TermId == query.TermId)
            .Select(r => new { r.PdfKey, r.Status, HomeroomTeacherId = r.Student!.Classroom!.HomeroomTeacherId })
            .FirstOrDefaultAsync(ct);
        if (reportCard?.PdfKey is null)
        {
            return Error.NotFound("report_cards.not_found", "There is no report card for this term yet.");
        }

        var allowed = currentUser.Role switch
        {
            UserRole.Student or UserRole.Parent => reportCard.Status == ReportCardStatus.Published && await ReportCardAccess.IsOwnAsync(db, currentUser, query.StudentId, ct),
            UserRole.Teacher => await SchoolMembership.TeacherIdAsync(db, currentUser, ct) == reportCard.HomeroomTeacherId,
            UserRole.Principal => true,
            _ => false,
        };
        if (!allowed)
        {
            return Error.Forbidden("report_cards.not_yours", "You may not open this report card.");
        }

        return new ReportCardUrlDto(
            storage.CreateDownloadUrl(reportCard.PdfKey, ReportCardAccess.DownloadLifetime),
            timeProvider.GetUtcNow() + ReportCardAccess.DownloadLifetime);
    }
}
