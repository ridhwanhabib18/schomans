import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime } from './format'

describe('date formatting', () => {
  it('shows DD/MM/YYYY', () => {
    expect(formatDate('2026-10-23T05:00:00Z')).toBe('23/10/2026')
  })

  it('uses Western Indonesia Time (UTC+7), even across midnight UTC', () => {
    // 20:00 UTC on the 23rd is already 03:00 on the 24th in WIB.
    expect(formatDate('2026-10-23T20:00:00Z')).toBe('24/10/2026')
    expect(formatDateTime('2026-10-23T20:00:00Z')).toBe('24/10/2026 03:00 WIB')
  })

  it('accepts Date objects and timestamps', () => {
    expect(formatDate(new Date('2027-01-04T00:00:00Z'))).toBe('04/01/2027')
    expect(formatDate(Date.UTC(2027, 0, 4))).toBe('04/01/2027')
  })
})
