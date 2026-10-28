import { describe, expect, it } from 'vitest'
import { checkInUrl, secondsUntil, tokenFromScan } from './checkIn'

const token = '0123456789abcdef0123456789abcdef.AbC_-123'

describe('checkInUrl', () => {
  it('points to the check-in page of the app with the code encoded', () => {
    expect(checkInUrl(token, 'https://school.example')).toBe(`https://school.example/attend?t=${encodeURIComponent(token)}`)
  })
})

describe('tokenFromScan', () => {
  it('finds the code in the check-in address', () => {
    expect(tokenFromScan(checkInUrl(token, 'https://school.example'))).toBe(token)
    expect(tokenFromScan(`  ${checkInUrl(token, 'http://localhost:5173')}\n`)).toBe(token)
  })

  it('accepts the bare code', () => {
    expect(tokenFromScan(token)).toBe(token)
  })

  it.each([
    'https://example.com/',
    'https://school.example/attend',
    'https://school.example/attend?t=nonsense',
    'https://school.example/other?t=0123456789abcdef0123456789abcdef.abc',
    'hello world',
    '',
  ])('refuses anything else: %s', (text) => {
    expect(tokenFromScan(text)).toBeNull()
  })
})

describe('secondsUntil', () => {
  it('counts whole seconds up and never goes below zero', () => {
    const now = new Date('2026-10-28T02:00:00Z').getTime()

    expect(secondsUntil('2026-10-28T02:01:00Z', now)).toBe(60)
    expect(secondsUntil('2026-10-28T02:00:00.100Z', now)).toBe(1)
    expect(secondsUntil('2026-10-28T01:59:00Z', now)).toBe(0)
  })
})
