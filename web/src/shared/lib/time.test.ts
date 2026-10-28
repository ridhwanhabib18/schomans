import { describe, expect, it } from 'vitest'
import { currentMonth, formatMonth, formatTime, isoToWibLocal, shiftMonth, toApiTime, todayWeekday, wibLocalToIso } from './time'

describe('lesson times', () => {
  it('shows a lesson time without seconds and sends it back with them', () => {
    expect(formatTime('07:00:00')).toBe('07:00')
    expect(toApiTime('07:00')).toBe('07:00:00')
    expect(toApiTime('07:00:00')).toBe('07:00:00')
  })
})

describe('months', () => {
  it('moves a month forward and back across the year boundary', () => {
    expect(shiftMonth('2026-01', -1)).toBe('2025-12')
    expect(shiftMonth('2026-12', 1)).toBe('2027-01')
    expect(shiftMonth('2026-10', 0)).toBe('2026-10')
    expect(shiftMonth('2026-03', -14)).toBe('2025-01')
  })

  it('names a month', () => {
    expect(formatMonth('2026-10')).toBe('October 2026')
  })

  it('takes the current month from the school time, not the browser time zone', () => {
    // 20:00 UTC on 31 October is already 03:00 on 1 November in Jakarta.
    expect(currentMonth(new Date('2026-10-31T20:00:00Z'))).toBe('2026-11')
    expect(currentMonth(new Date('2026-10-31T10:00:00Z'))).toBe('2026-10')
  })

  it('takes the weekday from the school time too', () => {
    expect(todayWeekday(new Date('2026-10-27T20:00:00Z'))).toBe('Wednesday') // Tuesday evening UTC, Wednesday morning in WIB
    expect(todayWeekday(new Date('2026-10-28T02:00:00Z'))).toBe('Wednesday')
  })
})

describe('due dates in school time', () => {
  it('reads a picked date and time as WIB, whatever the browser time zone is', () => {
    expect(wibLocalToIso('2026-10-30T23:59')).toBe('2026-10-30T16:59:00.000Z')
  })

  it('shows an instant as a WIB date and time for the input', () => {
    expect(isoToWibLocal('2026-10-30T16:59:00.000Z')).toBe('2026-10-30T23:59')
  })

  it('turns a value into an instant and back to the same value', () => {
    expect(isoToWibLocal(wibLocalToIso('2027-01-05T07:30'))).toBe('2027-01-05T07:30')
  })
})
