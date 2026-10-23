import { afterEach, describe, expect, it } from 'vitest'
import { makeToken } from '@/test/utils'
import { selectAuthRole, selectAuthStatus, selectAuthUser, useAuthStore } from './useAuthStore'

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('useAuthStore', () => {
  it('starts in the loading state, before the session check has finished', () => {
    expect(useAuthStore.getState().status).toBe('loading')
    expect(selectAuthUser(useAuthStore.getState())).toBeNull()
  })

  it('signs in from an access token and derives the role', () => {
    const accepted = useAuthStore
      .getState()
      .setSession(makeToken({ roles: ['Applicant', 'Teacher'], name: 'Budi Santoso', email: 'teacher@schomans.demo', sub: 'sub-1' }))

    const state = useAuthStore.getState()
    expect(accepted).toBe(true)
    expect(selectAuthStatus(state)).toBe('authenticated')
    expect(selectAuthRole(state)).toBe('Teacher')
    expect(selectAuthUser(state)).toEqual({ sub: 'sub-1', email: 'teacher@schomans.demo', name: 'Budi Santoso', role: 'Teacher' })
  })

  it('refuses a token without a SchoMans role and stays signed out', () => {
    const accepted = useAuthStore.getState().setSession(makeToken({ roles: ['offline_access'] }))

    expect(accepted).toBe(false)
    expect(useAuthStore.getState().status).toBe('anonymous')
    expect(useAuthStore.getState().accessToken).toBeNull()
  })

  it('clears the session', () => {
    useAuthStore.getState().setSession(makeToken())

    useAuthStore.getState().clearSession()

    expect(useAuthStore.getState()).toMatchObject({ status: 'anonymous', accessToken: null, user: null })
  })

  it('never writes the token to browser storage', () => {
    useAuthStore.getState().setSession(makeToken())

    expect(JSON.stringify({ ...localStorage })).not.toContain('signature')
    expect(JSON.stringify({ ...sessionStorage })).not.toContain('signature')
  })
})
