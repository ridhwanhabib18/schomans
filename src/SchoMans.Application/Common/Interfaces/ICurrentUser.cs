using SchoMans.Domain.Students;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>The signed-in person behind the current request, used for the ownership check of the second authorization layer.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string KeycloakId { get; }

    string Email { get; }

    string FullName { get; }

    UserRole Role { get; }
}
