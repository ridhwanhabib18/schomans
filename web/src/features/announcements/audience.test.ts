import { describe, expect, it } from 'vitest'
import { describeAudience, isTargetRole, SCHOOL_ROLES } from './audience'

describe('describeAudience', () => {
  it('means every school role, not applicants, when nothing is chosen', () => {
    const audience = describeAudience({ roles: [], classroomCount: 0 })

    expect(audience.kind).toBe('all')
    expect(audience.roles).toEqual(SCHOOL_ROLES)
    expect(audience.roles).not.toContain('Applicant')
  })

  it('lists the chosen roles in the order of the application, not the order they were picked', () => {
    const audience = describeAudience({ roles: ['Parent', 'Teacher'], classroomCount: 0 })

    expect(audience).toEqual({ kind: 'roles', roles: ['Teacher', 'Parent'], classroomCount: 0 })
  })

  it('describes classrooms without roles as the people of those classrooms', () => {
    expect(describeAudience({ roles: [], classroomCount: 2 }).kind).toBe('classrooms')
  })

  it('describes roles inside classrooms when both are chosen', () => {
    const audience = describeAudience({ roles: ['Parent'], classroomCount: 1 })

    expect(audience).toEqual({ kind: 'rolesInClassrooms', roles: ['Parent'], classroomCount: 1 })
  })
})

describe('isTargetRole', () => {
  it('accepts the six roles and nothing else', () => {
    expect(isTargetRole('Teacher')).toBe(true)
    expect(isTargetRole('Guest')).toBe(false)
    expect(isTargetRole('teacher')).toBe(false)
  })
})
