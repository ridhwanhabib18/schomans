import { describe, expect, it } from 'vitest'
import { APP_ROLES, isAppRole, resolveRole } from './roles'

describe('resolveRole', () => {
  it.each(APP_ROLES)('resolves %s', (role) => {
    expect(resolveRole([role])).toBe(role)
  })

  it('picks the most privileged role when a token carries several', () => {
    expect(resolveRole(['Applicant', 'Teacher', 'offline_access'])).toBe('Teacher')
    expect(resolveRole(['Admin', 'Principal'])).toBe('Principal')
    expect(resolveRole(['Student', 'Parent'])).toBe('Parent')
  })

  it('returns null when the token has no SchoMans role', () => {
    expect(resolveRole(['offline_access', 'uma_authorization', 'default-roles-schomans'])).toBeNull()
    expect(resolveRole([])).toBeNull()
  })

  it('is exact: it does not accept other casings or numbers', () => {
    expect(resolveRole(['teacher'])).toBeNull()
    expect(resolveRole(['1'])).toBeNull()
  })

  it('ignores surrounding whitespace', () => {
    expect(resolveRole([' Teacher '])).toBe('Teacher')
  })
})

describe('isAppRole', () => {
  it('knows the six realm roles and nothing else', () => {
    expect(APP_ROLES).toHaveLength(6)
    expect(isAppRole('Teacher')).toBe(true)
    expect(isAppRole('Guest')).toBe(false)
  })
})
