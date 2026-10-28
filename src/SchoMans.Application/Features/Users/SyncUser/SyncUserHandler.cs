using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Users.SyncUser;

public sealed record SyncedUserDto(Guid UserId, string KeycloakId, string Email, string FullName, UserRole Role);

/// <summary>
/// Keeps the User table in step with Keycloak. Looks the person up by <c>sub</c>, then by email (an account that
/// staff created before the first login), and creates an account if neither exists (a self-registered applicant).
/// </summary>
public sealed class SyncUserHandler(IAppDbContext db)
{
    public async Task<Result<SyncedUserDto>> Handle(SyncUserCommand command, CancellationToken ct)
    {
        if (!UserRoleResolver.TryResolve(command.RealmRoles, out var role))
        {
            return Error.Forbidden("auth.no_application_role", "The account has no SchoMans role.");
        }

        // Two first requests of the same new user can race on the unique indexes; the loser reads the winner's row.
        for (var attempt = 1; ; attempt++)
        {
            var result = await TrySync(command, role, ct);
            if (result is not null)
            {
                return result;
            }

            if (attempt == 2)
            {
                return Error.Conflict("auth.sync_conflict", "The account could not be synchronized, please retry.");
            }
        }
    }

    private async Task<Result<SyncedUserDto>?> TrySync(SyncUserCommand command, UserRole role, CancellationToken ct)
    {
        // A person whose record was deleted (for example a student who left) must not come back by logging in again.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.KeycloakId == command.Sub && u.IsDeleted, ct))
        {
            return Error.Forbidden("auth.account_deactivated", "This account has been deactivated.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakId == command.Sub, ct);

        if (user is null)
        {
            var email = User.NormalizeEmail(command.Email);
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is not null)
            {
                // Linked to the same sub is the same person (a parallel request just created the row); only a
                // different sub means the email belongs to someone else.
                if (user.KeycloakId is not null && user.KeycloakId != command.Sub)
                {
                    return Error.Conflict("auth.email_already_linked", "This email belongs to a different account.");
                }

                user.LinkKeycloak(command.Sub);
            }
        }

        if (user is null)
        {
            user = User.Create(command.Email, command.FullName, role, command.Sub);
            db.Users.Add(user);
        }
        else
        {
            user.SyncProfile(command.Email, command.FullName, role);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Forget the failed insert, or the retry would try to save the same row again and fail again.
            db.ChangeTracker.Clear();
            return null;
        }

        return new SyncedUserDto(user.Id, command.Sub, user.Email, user.FullName, user.Role);
    }
}
