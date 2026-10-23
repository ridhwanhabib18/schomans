using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Users.GetMe;

public sealed record MeDto(Guid Id, string Email, string FullName, string Role, bool IsHomeroomTeacher);

public sealed class GetMeHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<MeDto>> Handle(GetMeQuery query, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

        if (user is null)
        {
            return Error.NotFound("users.not_found", "The signed-in user does not exist.");
        }

        // A teacher can also be the homeroom teacher of a classroom.
        var isHomeroomTeacher = await db.Classrooms
            .AnyAsync(c => c.HomeroomTeacher!.UserId == user.Id, ct);

        return new MeDto(user.Id, user.Email, user.FullName, user.Role.ToString(), isHomeroomTeacher);
    }
}
