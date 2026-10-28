using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students.ListMyChildren;

public sealed record ChildDto(Guid StudentId, string FullName, string ClassroomName);

/// <summary>The children linked to the signed-in parent, for the child picker of the parent pages.</summary>
public sealed class ListMyChildrenHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ChildDto>>> Handle(CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Parent)
        {
            return Error.Forbidden("students.not_a_parent", "Only parents have children linked to their account.");
        }

        return await db.StudentParents.AsNoTracking()
            .Where(p => p.ParentUserId == currentUser.UserId)
            .OrderBy(p => p.Student!.User!.FullName)
            .Select(p => new ChildDto(p.StudentId, p.Student!.User!.FullName, p.Student.Classroom!.Name))
            .ToListAsync(ct);
    }
}
