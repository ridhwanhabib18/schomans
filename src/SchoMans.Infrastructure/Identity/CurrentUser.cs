using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Users.SyncUser;
using SchoMans.Domain.Students;

namespace SchoMans.Infrastructure.Identity;

/// <summary>Per-request holder of the signed-in user. The API fills it once the token has been synced to the User table.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private SyncedUserDto? _user;

    public bool IsAuthenticated => _user is not null;

    public Guid UserId => Required.UserId;

    public string KeycloakId => Required.KeycloakId;

    public string Email => Required.Email;

    public string FullName => Required.FullName;

    public UserRole Role => Required.Role;

    private SyncedUserDto Required =>
        _user ?? throw new InvalidOperationException("There is no signed-in user for this request.");

    public void Set(SyncedUserDto user) => _user = user;
}
