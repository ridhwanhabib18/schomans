import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { AdmissionPeriod } from '../api/admissions'
import { AdmissionPeriodsPage } from './AdmissionPeriodsPage'

const api = vi.hoisted(() => ({ listAcademicYears: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

type Config = { url: string; method: string; data?: Record<string, unknown> }
type Handler = (config: Config) => unknown

const period = (overrides: Partial<AdmissionPeriod> = {}): AdmissionPeriod => ({
  id: 'p1',
  name: 'Admissions 2027/2028',
  academicYearId: 'y1',
  academicYearName: '2026/2027',
  opensAt: '2026-10-01T00:00:00Z',
  closesAt: '2026-12-01T00:00:00Z',
  resultsAnnouncedAt: '2026-12-15T03:00:00Z',
  reregistrationDeadline: '2027-01-05T03:00:00Z',
  applicantCount: 4,
  ...overrides,
})

function serve(handlers: Record<string, Handler>, periods: AdmissionPeriod[] = [period()]) {
  http.customInstance.mockImplementation(async (config: Config) => {
    const key = `${config.method} ${config.url.replace('/api/v1/admissions', '')}`
    if (key === 'GET /periods') return periods
    const handler = handlers[key]
    if (!handler) throw new Error(`unexpected request ${key}`)
    return handler(config)
  })
}

const calls = (key: string) =>
  http.customInstance.mock.calls.map(([config]) => config as Config).filter((config) => `${config.method} ${config.url.replace('/api/v1/admissions', '')}` === key)

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

/** Fills the dates of the form; the values are school time (WIB), like the browser's date-time fields. */
function fillDates(dialog: HTMLElement, dates: [string, string, string, string]) {
  const labels = [/^Applications open/, /^Applications close/, /^Results announced/, /^Re-registration deadline/]
  labels.forEach((label, index) => fireEvent.change(within(dialog).getByLabelText(label), { target: { value: dates[index] } }))
}

const inOrder: [string, string, string, string] = ['2027-10-01T07:00', '2027-12-01T07:00', '2027-12-15T10:00', '2028-01-05T10:00']

beforeEach(() => {
  api.listAcademicYears.mockReset().mockResolvedValue({ items: [{ id: 'y1', name: '2026/2027', isActive: true }, { id: 'y2', name: '2027/2028', isActive: false }] })
  http.customInstance.mockReset()
  useToastStore.setState({ toasts: [] })
  signInAs('Admin', 'M Ridhwan Habib A')
})

describe('AdmissionPeriodsPage', () => {
  it('lists the periods with their dates in school time and the number of applications', async () => {
    serve({})
    renderPage(<AdmissionPeriodsPage />)

    expect(await screen.findByRole('cell', { name: 'Admissions 2027/2028' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: '2026/2027' })).toBeTruthy()
    expect(screen.getByText('01/10/2026 07:00 WIB – 01/12/2026 07:00 WIB')).toBeTruthy()
    expect(screen.getByRole('cell', { name: '4' })).toBeTruthy()
  })

  it('shows an empty state', async () => {
    serve({}, [])
    renderPage(<AdmissionPeriodsPage />)

    expect(await screen.findByText('No admission periods')).toBeTruthy()
  })

  it('creates a period: the dates are sent as instants in school time, with the chosen academic year', async () => {
    serve({ 'POST /periods': () => period({ id: 'p2' }) })
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'New period' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'Admissions 2028/2029' } })
    await waitFor(() => expect(within(dialog).getByRole('option', { name: '2027/2028' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/^Academic year/), { target: { value: 'y2' } })
    fillDates(dialog, inOrder)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Period saved']))
    expect(calls('POST /periods')[0]?.data).toEqual({
      academicYearId: 'y2',
      name: 'Admissions 2028/2029',
      opensAt: '2027-10-01T00:00:00.000Z',
      closesAt: '2027-12-01T00:00:00.000Z',
      resultsAnnouncedAt: '2027-12-15T03:00:00.000Z',
      reregistrationDeadline: '2028-01-05T03:00:00.000Z',
    })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('does not send dates that are out of order', async () => {
    serve({})
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'New period' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'Admissions' } })
    await waitFor(() => expect(within(dialog).getByRole('option', { name: '2026/2027' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/^Academic year/), { target: { value: 'y1' } })
    fillDates(dialog, ['2027-10-01T07:00', '2027-09-01T07:00', '2027-12-15T10:00', '2028-01-05T10:00'])
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect((await within(dialog).findByRole('alert')).textContent).toContain('The dates must run in order')
    expect(calls('POST /periods')).toHaveLength(0)
  })

  it('shows an overlap with another period inside the form and keeps the form open', async () => {
    serve({
      'POST /periods': () => {
        throw apiError(409, { code: 'admissions.period_overlaps' })
      },
    })
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'New period' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'Admissions' } })
    await waitFor(() => expect(within(dialog).getByRole('option', { name: '2026/2027' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/^Academic year/), { target: { value: 'y1' } })
    fillDates(dialog, inOrder)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect((await within(dialog).findByRole('alert')).textContent).toContain('Another admission period is open at the same time.')
    expect(screen.getByRole('dialog')).toBeTruthy()
  })

  it('edits the dates of a period without asking for the academic year again', async () => {
    serve({ 'PUT /periods/p1': () => period({ name: 'Admissions renamed' }) })
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Edit: Admissions 2027/2028' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).queryByLabelText(/^Academic year/)).toBeNull()
    expect((within(dialog).getByLabelText(/^Applications open/) as HTMLInputElement).value).toBe('2026-10-01T07:00')
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'Admissions renamed' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Period saved']))
    expect(calls('PUT /periods/p1')[0]?.data).toMatchObject({ id: 'p1', name: 'Admissions renamed', opensAt: '2026-10-01T00:00:00.000Z' })
  })

  it('deletes a period after a confirmation', async () => {
    serve({ 'DELETE /periods/p1': () => undefined })
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Delete: Admissions 2027/2028' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(calls('DELETE /periods/p1')).toHaveLength(0)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Period deleted']))
  })

  it('says why a period with applications cannot be deleted', async () => {
    serve({
      'DELETE /periods/p1': () => {
        throw apiError(409, { code: 'admissions.period_in_use' })
      },
    })
    renderPage(<AdmissionPeriodsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Delete: Admissions 2027/2028' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(toasts()).toEqual(['error:This period has applications, so it cannot be deleted.']))
  })

  it('shows an error with a retry when the periods cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    renderPage(<AdmissionPeriodsPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The admission periods could not be loaded.')
  })
})
