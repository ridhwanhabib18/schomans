/**
 * Times of the school day and months, in Western Indonesia Time (WIB) like every date the app shows. The API sends
 * lesson times as "07:00:00" (local school time) and months as "2026-10".
 */
const timeZone = 'Asia/Jakarta'

/** "07:00:00" → "07:00". */
export function formatTime(value: string): string {
  return value.slice(0, 5)
}

/** "07:00" or "07:00:00" → "07:00:00", the form the API expects. */
export function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value
}

const monthPart = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit' })

/** The current month in WIB as "2026-10". */
export function currentMonth(now: Date = new Date()): string {
  return monthPart.format(now).slice(0, 7)
}

/** "2026-10" moved by a number of months: ("2026-01", -1) → "2025-12". */
export function shiftMonth(month: string, by: number): string {
  const [year, number] = month.split('-').map(Number) as [number, number]
  const index = year * 12 + (number - 1) + by
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}`
}

const monthName = new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric', timeZone: 'UTC' })

/** "2026-10" → "October 2026". */
export function formatMonth(month: string): string {
  const [year, number] = month.split('-').map(Number) as [number, number]
  return monthName.format(new Date(Date.UTC(year, number - 1, 1)))
}

const weekday = new Intl.DateTimeFormat('en-US', { timeZone, weekday: 'long' })

/** Today's weekday in WIB, as the API names it ("Monday"). */
export function todayWeekday(now: Date = new Date()): string {
  return weekday.format(now)
}

/** The school week, Monday first. */
export const WEEK_DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const

/**
 * A due date picked in the browser (a `datetime-local` value, "2026-10-30T23:59") as an instant. The value is read as
 * WIB, the school's time, so a teacher abroad still sets the deadline in school time.
 */
export function wibLocalToIso(value: string): string {
  return new Date(`${value}:00+07:00`).toISOString()
}

/** The reverse of {@link wibLocalToIso}: an instant as a `datetime-local` value in WIB. */
export function isoToWibLocal(value: string): string {
  const shifted = new Date(new Date(value).getTime() + 7 * 60 * 60 * 1000)
  return shifted.toISOString().slice(0, 16)
}
