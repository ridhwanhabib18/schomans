namespace SchoMans.Application.Features.Users.SyncUser;

/// <summary>The identity claims of a validated Keycloak token.</summary>
public sealed record SyncUserCommand(string Sub, string Email, string FullName, IReadOnlyCollection<string> RealmRoles);
