using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.ReportCards;

internal static class ReportCardAccess
{
    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    public static string PdfKey(Guid termId, Guid studentId) => $"report-cards/{termId:N}/{studentId:N}.pdf";

    /// <summary>Students and their parents (family): see grades and report cards only once they are published.</summary>
    public static bool IsFamily(UserRole role) => role is UserRole.Student or UserRole.Parent;

    /// <summary>True when the student is the signed-in student or a child of the signed-in parent.</summary>
    public static async Task<bool> IsOwnAsync(IAppDbContext db, ICurrentUser user, Guid studentId, CancellationToken ct) =>
        user.Role switch
        {
            UserRole.Student => await SchoolMembership.StudentIdAsync(db, user, ct) == studentId,
            UserRole.Parent => await db.StudentParents.AnyAsync(p => p.ParentUserId == user.UserId && p.StudentId == studentId, ct),
            _ => false,
        };
}
