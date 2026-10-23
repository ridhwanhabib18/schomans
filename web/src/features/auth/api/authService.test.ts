import axios from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeToken } from '@/test/utils'
import { useAuthStore } from '@/stores/useAuthStore'
import { NoRoleError, demoLogin, logout } from './authService'

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(async () => {
  await logout()
  vi.useRealTimers()
  vi.restoreAllMocks()
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('demoLogin', () => {
  it('requests a token for the demo user of the role and signs in', async () => {
    const post = vi
      .spyOn(axios, 'post')
      .mockResolvedValue({ data: { access_token: makeToken({ roles: ['Teacher'], name: 'Budi Santoso' }), refresh_token: 'r1', expires_in: 900 } })

    await demoLogin('Teacher')

    const [url, body] = post.mock.calls[0] as [string, URLSearchParams]
    expect(url).toContain('/realms/schomans/protocol/openid-connect/token')
    expect(body.get('grant_type')).toBe('password')
    expect(body.get('username')).toBe('teacher@schomans.demo')
    expect(body.get('client_id')).toBe('schomans-web-demo')
    expect(useAuthStore.getState().user?.role).toBe('Teacher')
  })

  it('refuses an account that holds no SchoMans role', async () => {
    vi.spyOn(axios, 'post').mockResolvedValue({ data: { access_token: makeToken({ roles: ['offline_access'] }), expires_in: 900 } })

    await expect(demoLogin('Student')).rejects.toBeInstanceOf(NoRoleError)

    expect(useAuthStore.getState().status).toBe('anonymous')
  })

  it('renews the token with the refresh token shortly before it expires', async () => {
    const post = vi
      .spyOn(axios, 'post')
      .mockResolvedValueOnce({ data: { access_token: makeToken({ roles: ['Student'] }), refresh_token: 'r1', expires_in: 900 } })
      .mockResolvedValueOnce({ data: { access_token: makeToken({ roles: ['Student'], name: 'Renewed' }), refresh_token: 'r2', expires_in: 900 } })

    await demoLogin('Student')
    await vi.advanceTimersByTimeAsync(871 * 1000)

    expect(post).toHaveBeenCalledTimes(2)
    expect((post.mock.calls[1]?.[1] as URLSearchParams).get('grant_type')).toBe('refresh_token')
    expect((post.mock.calls[1]?.[1] as URLSearchParams).get('refresh_token')).toBe('r1')
    expect(useAuthStore.getState().user?.name).toBe('Renewed')
  })

  it('signs out when the renewal fails', async () => {
    vi.spyOn(axios, 'post')
      .mockResolvedValueOnce({ data: { access_token: makeToken({ roles: ['Student'] }), refresh_token: 'r1', expires_in: 900 } })
      .mockRejectedValueOnce(new Error('refresh token expired'))

    await demoLogin('Student')
    await vi.advanceTimersByTimeAsync(871 * 1000)

    expect(useAuthStore.getState().status).toBe('anonymous')
  })
})

describe('logout', () => {
  it('ends a demo session without leaving the app', async () => {
    vi.spyOn(axios, 'post').mockResolvedValue({ data: { access_token: makeToken({ roles: ['Admin'] }), expires_in: 900 } })
    await demoLogin('Admin')

    await logout()

    expect(useAuthStore.getState()).toMatchObject({ status: 'anonymous', accessToken: null, user: null })
  })
})
