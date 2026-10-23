import { describe, expect, it } from 'vitest'
import { makeToken } from '@/test/utils'
import { decodeAccessToken } from './jwt'

describe('decodeAccessToken', () => {
  it('reads the claims of a token', () => {
    const claims = decodeAccessToken(makeToken({ roles: ['Teacher'], name: 'Budi Santoso', email: 'teacher@schomans.demo', sub: 'abc' }))

    expect(claims.sub).toBe('abc')
    expect(claims.name).toBe('Budi Santoso')
    expect(claims.email).toBe('teacher@schomans.demo')
    expect(claims.realm_access?.roles).toEqual(['Teacher'])
  })

  it('handles non-ASCII names', () => {
    expect(decodeAccessToken(makeToken({ name: 'Sinta Maharani — Çelik' })).name).toBe('Sinta Maharani — Çelik')
  })

  it('rejects something that is not a JWT', () => {
    expect(() => decodeAccessToken('not-a-token')).toThrow()
  })
})
