# ADR-003: Changing a role through the Keycloak Admin API

- Status: Accepted
- Date: 2026-11-02

## Context
The role of a person is a realm role in Keycloak and the API takes it from the token; a login overwrites the role stored in the
database. An applicant who is enrolled (F-07) must become a student, so changing the database alone would be undone at the
next login, and the token would keep saying Applicant.

## Decision
When the admin staff enroll an applicant, the API gives the account the realm role Student and takes the realm role Applicant
away through the Keycloak Admin REST API, signed in as a service account (client `schomans-admin-api`, client credentials).
Only then does it create the student and change the role in the database. The call sits behind `IIdentityProvisioner`.

## Reasons
- Keycloak stays the one authority for roles; the database never disagrees with the token.
- The service account holds only `manage-users` and `view-realm`, nothing for clients or the realm settings.
- No new package: a typed `HttpClient` is enough for four calls.

## Consequences
- A new confidential client in the realm file (`infra/keycloak/realm-schomans.json`) and the settings `KeycloakAdmin:*`; the
  secret in the realm file is for local development only and must be replaced elsewhere.
- `manage-users` also lets the account create users: Keycloak has no narrower right for role mappings without its fine-grained
  admin permissions.
- When Keycloak is down the enrollment fails with 502 and changes nothing; it can be tried again (the swap is idempotent).
- A signed-in applicant keeps the old token until it is refreshed or they sign in again.
