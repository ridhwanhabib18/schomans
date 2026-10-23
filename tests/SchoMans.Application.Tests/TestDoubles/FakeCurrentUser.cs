using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Tests.TestDoubles;

internal sealed class FakeCurrentUser(UserRole role = UserRole.Teacher, string fullName = "Budi Santoso") : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid UserId { get; } = Guid.NewGuid();

    public string KeycloakId => "sub-test";

    public string Email => "teacher@schomans.demo";

    public string FullName { get; } = fullName;

    public UserRole Role { get; } = role;
}
