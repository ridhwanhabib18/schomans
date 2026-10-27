import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { AcademicYearsPage } from './AcademicYearsPage'

const api = vi.hoisted(() => ({
  listAcademicYears: vi.fn(),
  createAcademicYear: vi.fn(),
  updateAcademicYear: vi.fn(),
  activateAcademicYear: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const term = (overrides: Record<string, unknown> = {}) => ({ id: 'tm1', name: 'Odd', startDate: '2026-07-13', endDate: '2026-12-19', isActive: true, ...overrides })

const year = (overrides: Record<string, unknown> = {}) => ({
  id: 'y1',
  name: '2026/2027',
  isActive: true,
  startDate: '2026-07-13',
  endDate: '2027-06-26',
  terms: [term(), term({ id: 'tm2', name: 'Even', startDate: '2027-01-04', endDate: '2027-06-26', isActive: false })],
  classroomCount: 3,
  rowVersion: 'rv',
  ...overrides,
})

const renderYears = () =>
  renderRoutes(
    [
      { path: '/', element: <AcademicYearsPage /> },
      { path: '/manage/academic-years/rollover', element: <p>Rollover wizard</p> },
    ],
    '/',
  )

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listAcademicYears.mockResolvedValue(pageOf([year(), year({ id: 'y2', name: '2027/2028', isActive: false, terms: [term({ id: 'tm3', name: 'Odd', isActive: false })] })]))
  api.createAcademicYear.mockResolvedValue({ id: 'y3' })
  api.updateAcademicYear.mockResolvedValue(undefined)
  api.activateAcademicYear.mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('AcademicYearsPage', () => {
  it('lists the years with their terms and marks the current one', async () => {
    signInAs('Teacher')
    renderYears()

    expect(await screen.findByText('2026/2027')).toBeTruthy()
    expect(screen.getByText('Odd, Even')).toBeTruthy()
    expect(screen.getAllByText('Current')).toHaveLength(1)
  })

  it('shows a teacher no way to change anything', async () => {
    signInAs('Teacher')
    renderYears()
    await screen.findByText('2026/2027')

    expect(screen.queryByRole('button', { name: 'Add academic year' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Start a new year' })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Edit/ })).toBeNull()
  })

  it('leads the admin to the rollover wizard', async () => {
    signInAs('Admin')
    renderYears()

    fireEvent.click(await screen.findByRole('link', { name: 'Start a new year' }))

    expect(await screen.findByText('Rollover wizard')).toBeTruthy()
  })

  it('makes another year current only after the admin confirms', async () => {
    signInAs('Admin')
    renderYears()
    await screen.findByText('2026/2027')

    expect(screen.getAllByRole('button', { name: /Make current/ })).toHaveLength(1)
    fireEvent.click(screen.getByRole('button', { name: /Make current/ }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Make 2027/2028 the current academic year?')).toBeTruthy()
    expect(api.activateAcademicYear).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Make current' }))

    await waitFor(() => expect(api.activateAcademicYear).toHaveBeenCalledWith('y2'))
    await waitFor(() => expect(toasts()).toEqual(['success:2027/2028 is now the current academic year.']))
  })

  it('creates a year with its terms', async () => {
    signInAs('Admin')
    renderYears()
    fireEvent.click(await screen.findByRole('button', { name: 'Add academic year' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add academic year' })
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: '2028/2029' } })
    fireEvent.change(within(dialog).getByLabelText(/Starts on/), { target: { value: '2028-07-10' } })
    fireEvent.change(within(dialog).getByLabelText(/Ends on/), { target: { value: '2029-06-23' } })
    fireEvent.change(within(dialog).getByLabelText(/Term name/), { target: { value: 'Odd' } })
    fireEvent.change(within(dialog).getByLabelText(/Term starts on/), { target: { value: '2028-07-10' } })
    fireEvent.change(within(dialog).getByLabelText(/Term ends on/), { target: { value: '2028-12-16' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(api.createAcademicYear).toHaveBeenCalledWith({
        name: '2028/2029',
        startDate: '2028-07-10',
        endDate: '2029-06-23',
        terms: [{ id: null, name: 'Odd', startDate: '2028-07-10', endDate: '2028-12-16' }],
      }),
    )
  })

  it('adds and removes terms, up to four, and keeps at least one', async () => {
    signInAs('Admin')
    renderYears()
    fireEvent.click(await screen.findByRole('button', { name: 'Add academic year' }))
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).queryByRole('button', { name: 'Remove term' })).toBeNull()
    for (let i = 0; i < 3; i++) fireEvent.click(within(dialog).getByRole('button', { name: 'Add term' }))
    expect(within(dialog).getAllByLabelText(/Term name/)).toHaveLength(4)
    expect(within(dialog).queryByRole('button', { name: 'Add term' })).toBeNull()

    fireEvent.click(within(dialog).getAllByRole('button', { name: 'Remove term' })[0]!)
    expect(within(dialog).getAllByLabelText(/Term name/)).toHaveLength(3)
  })

  it('does not offer to remove a term that is already saved', async () => {
    signInAs('Admin')
    renderYears()
    await screen.findByText('2026/2027')

    fireEvent.click(screen.getAllByRole('button', { name: /^Edit/ })[0]!)
    const dialog = await screen.findByRole('dialog', { name: 'Edit academic year' })

    expect(within(dialog).getAllByLabelText(/Term name/)).toHaveLength(2)
    expect(within(dialog).queryByRole('button', { name: 'Remove term' })).toBeNull()
  })

  it('sends the ids of saved terms when the year is updated', async () => {
    signInAs('Admin')
    renderYears()
    await screen.findByText('2026/2027')

    fireEvent.click(screen.getAllByRole('button', { name: /^Edit/ })[0]!)
    const dialog = await screen.findByRole('dialog', { name: 'Edit academic year' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.updateAcademicYear).toHaveBeenCalledTimes(1))
    const [id, command] = api.updateAcademicYear.mock.calls[0]!
    expect(id).toBe('y1')
    expect(command.rowVersion).toBe('rv')
    expect(command.terms.map((t: { id: string }) => t.id)).toEqual(['tm1', 'tm2'])
  })

  it('explains an invalid set of dates', async () => {
    signInAs('Admin')
    api.createAcademicYear.mockRejectedValue(apiError(400, { code: 'academic_years.invalid' }))
    renderYears()
    fireEvent.click(await screen.findByRole('button', { name: 'Add academic year' }))
    const dialog = await screen.findByRole('dialog')

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('The dates are not valid: terms must be inside the year and have different names.')).toBeTruthy()
  })
})
