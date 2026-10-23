/** The six Keycloak realm roles. A Guest is anonymous and has no role. */
export const APP_ROLES = ['Principal', 'Teacher', 'Student', 'Parent', 'Admin', 'Applicant'] as const

export type AppRole = (typeof APP_ROLES)[number]

// Highest privilege first. Mirrors UserRoleResolver in the backend: a token can carry several roles
// (for example the default Applicant role next to a real one) and the most privileged one wins.
const PRIORITY: readonly AppRole[] = ['Principal', 'Admin', 'Teacher', 'Parent', 'Student', 'Applicant']

export function isAppRole(value: string): value is AppRole {
  return (APP_ROLES as readonly string[]).includes(value)
}

/** Picks the single application role out of the realm roles of a token, or null when it has none. */
export function resolveRole(realmRoles: readonly string[]): AppRole | null {
  const known = new Set(realmRoles.map((role) => role.trim()).filter(isAppRole))
  return PRIORITY.find((role) => known.has(role)) ?? null
}
