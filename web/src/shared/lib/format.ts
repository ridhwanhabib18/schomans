/** Dates are shown as DD/MM/YYYY in Western Indonesia Time (WIB), whatever the browser's own settings are. */
const timeZone = 'Asia/Jakarta'

const dateFormat = new Intl.DateTimeFormat('en-GB', { timeZone, day: '2-digit', month: '2-digit', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('en-GB', {
  timeZone,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

export function formatDate(value: Date | string | number): string {
  return dateFormat.format(new Date(value))
}

export function formatDateTime(value: Date | string | number): string {
  return `${dateTimeFormat.format(new Date(value))} WIB`.replace(',', '')
}
