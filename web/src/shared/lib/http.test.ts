import { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeToken } from '@/test/utils'
import { useAuthStore } from '@/stores/useAuthStore'
import { customInstance, http } from './http'

const originalAdapter = http.defaults.adapter

afterEach(() => {
  http.defaults.adapter = originalAdapter
  useAuthStore.getState().clearSession()
})

describe('http client', () => {
  let seen: InternalAxiosRequestConfig | undefined

  beforeEach(() => {
    seen = undefined
    http.defaults.adapter = async (config) => {
      seen = config
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config }
    }
  })

  it('sends the access token as a bearer token', async () => {
    const token = makeToken({ roles: ['Teacher'] })
    useAuthStore.getState().setSession(token)

    await customInstance({ url: '/api/v1/dashboard', method: 'GET' })

    expect(seen?.headers.get('Authorization')).toBe(`Bearer ${token}`)
  })

  it('sends no Authorization header when nobody is signed in', async () => {
    useAuthStore.getState().clearSession()

    await customInstance({ url: '/api/v1/dashboard', method: 'GET' })

    expect(seen?.headers.get('Authorization')).toBeFalsy()
  })

  it('returns only the response body', async () => {
    await expect(customInstance({ url: '/x', method: 'GET' })).resolves.toEqual({ ok: true })
  })

  it('drops the session when the API answers 401', async () => {
    useAuthStore.getState().setSession(makeToken())
    http.defaults.adapter = async (config) => {
      throw new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        headers: {},
        config,
        data: null,
      })
    }

    await expect(customInstance({ url: '/x', method: 'GET' })).rejects.toBeInstanceOf(AxiosError)

    expect(useAuthStore.getState().status).toBe('anonymous')
    expect(useAuthStore.getState().accessToken).toBeNull()
  })

  it('keeps the session when the API answers 403', async () => {
    useAuthStore.getState().setSession(makeToken())
    http.defaults.adapter = async (config) => {
      throw new AxiosError('Forbidden', 'ERR_BAD_REQUEST', config, null, {
        status: 403,
        statusText: 'Forbidden',
        headers: {},
        config,
        data: null,
      })
    }

    await expect(customInstance({ url: '/x', method: 'GET' })).rejects.toBeInstanceOf(AxiosError)

    expect(useAuthStore.getState().status).toBe('authenticated')
  })
})
