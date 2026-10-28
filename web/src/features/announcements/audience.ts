import { APP_ROLES, type AppRole } from '@/shared/lib/roles'

/** The roles an announcement without a role selection reaches: everybody in the school, but not applicants. */
export const SCHOOL_ROLES: readonly AppRole[] = ['Principal', 'Teacher', 'Student', 'Parent', 'Admin']

export interface Audience {
  roles: readonly AppRole[]
  classroomCount: number
}

export interface AudienceDescription {
  /** `all` = every school role; `roles` = the named roles; `classrooms` = people belonging to the chosen classrooms. */
  kind: 'all' | 'roles' | 'classrooms' | 'rolesInClassrooms'
  roles: readonly AppRole[]
  classroomCount: number
}

/**
 * Describes who an announcement is for, using the same rules as the API: no roles means every school role, no
 * classrooms means no restriction by classroom, and a classroom reaches its students, their parents and its homeroom teacher.
 */
export function describeAudience({ roles, classroomCount }: Audience): AudienceDescription {
  const ordered = APP_ROLES.filter((role) => roles.includes(role))
  if (ordered.length === 0 && classroomCount === 0) {
    return { kind: 'all', roles: SCHOOL_ROLES, classroomCount }
  }

  if (classroomCount === 0) {
    return { kind: 'roles', roles: ordered, classroomCount }
  }

  return { kind: ordered.length === 0 ? 'classrooms' : 'rolesInClassrooms', roles: ordered, classroomCount }
}

/** True for a role the API accepts as a target. */
export function isTargetRole(value: string): value is AppRole {
  return (APP_ROLES as readonly string[]).includes(value)
}
