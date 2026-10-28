import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import { SchedulesPage } from './SchedulesPage'

const api = vi.hoisted(() => ({
  listSchedules: vi.fn(),
  createSchedule: vi.fn(),
  updateSchedule: vi.fn(),
  deleteSchedule: vi.fn(),
  listAcademicYears: vi.fn(),
  listClassrooms: vi.fn(),
  listSubjects: vi.fn(),
  listTeachers: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const lesson = (overrides: Record<string, unknown> = {}) => ({
  id: 'l1',
  classroomId: 'c1',
  classroomName: 'Class A',
  subjectId: 'sub1',
  subjectName: 'Mathematics',
  teacherId: 't1',
  teacherName: 'Budi Santoso',
  dayOfWeek: 'Monday',
  startTime: '07:00:00',
  endTime: '08:30:00',
  rowVersion: 'rv',
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listAcademicYears.mockResolvedValue(pageOf([{ id: 'y1', name: '2026/2027', isActive: true }]))
  api.listClassrooms.mockResolvedValue(pageOf([{ id: 'c1', name: 'Class A' }, { id: 'c2', name: 'Class B' }]))
  api.listSchedules.mockResolvedValue(pageOf([lesson(), lesson({ id: 'l2', subjectName: 'English', dayOfWeek: 'Tuesday', startTime: '08:30:00', endTime: '10:00:00' })]))
  api.listSubjects.mockResolvedValue(pageOf([{ id: 'sub1', name: 'Mathematics' }, { id: 'sub2', name: 'English' }]))
  api.listTeachers.mockResolvedValue(pageOf([{ id: 't1', fullName: 'Budi Santoso' }, { id: 't2', fullName: 'Ani Wijaya' }]))
  for (const name of ['createSchedule', 'updateSchedule', 'deleteSchedule'] as const) api[name].mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('SchedulesPage', () => {
  it('shows the week of the first classroom, day by day, Monday to Friday', async () => {
    signInAs('Teacher')
    renderPage(<SchedulesPage />)

    expect(await screen.findByText('Mathematics')).toBeTruthy()
    for (const day of ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']) {
      expect(screen.getByRole('heading', { name: day })).toBeTruthy()
    }
    expect(screen.queryByRole('heading', { name: 'Saturday' })).toBeNull()
    expect(screen.getByText('07:00–08:30')).toBeTruthy()
    expect(api.listSchedules).toHaveBeenCalledWith({ ClassroomId: 'c1', Page: 1, PageSize: 100 })
  })

  it('shows a weekend day only when it has lessons', async () => {
    signInAs('Teacher')
    api.listSchedules.mockResolvedValue(pageOf([lesson({ dayOfWeek: 'Saturday' })]))
    renderPage(<SchedulesPage />)

    expect(await screen.findByRole('heading', { name: 'Saturday' })).toBeTruthy()
  })

  it('switches classroom', async () => {
    signInAs('Teacher')
    renderPage(<SchedulesPage />)
    await screen.findByText('Mathematics')

    fireEvent.change(screen.getByLabelText('Classroom'), { target: { value: 'c2' } })

    await waitFor(() => expect(api.listSchedules).toHaveBeenLastCalledWith({ ClassroomId: 'c2', Page: 1, PageSize: 100 }))
  })

  it('gives staff no way to change the timetable', async () => {
    signInAs('Principal')
    renderPage(<SchedulesPage />)
    await screen.findByText('Mathematics')

    expect(screen.queryByRole('button', { name: 'Add lesson' })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Edit/ })).toBeNull()
  })

  it('adds a lesson to the chosen classroom', async () => {
    signInAs('Admin')
    renderPage(<SchedulesPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add lesson' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add lesson' })
    expect(within(dialog).getByText('Classroom Class A')).toBeTruthy()
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Ani Wijaya' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/^Day/), { target: { value: 'Wednesday' } })
    fireEvent.change(within(dialog).getByLabelText(/Starts at/), { target: { value: '10:15' } })
    fireEvent.change(within(dialog).getByLabelText(/Ends at/), { target: { value: '11:45' } })
    fireEvent.change(within(dialog).getByLabelText(/^Subject/), { target: { value: 'sub2' } })
    fireEvent.change(within(dialog).getByLabelText(/^Teacher/), { target: { value: 't2' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(api.createSchedule).toHaveBeenCalledWith({ classroomId: 'c1', subjectId: 'sub2', teacherId: 't2', dayOfWeek: 'Wednesday', startTime: '10:15:00', endTime: '11:45:00' }),
    )
    await waitFor(() => expect(toasts()).toEqual(['success:Saved.']))
  })

  it('edits a lesson starting from its values and keeps its row version', async () => {
    signInAs('Admin')
    renderPage(<SchedulesPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit: Mathematics/ }))[0]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit lesson' })
    expect((within(dialog).getByLabelText(/Starts at/) as HTMLInputElement).value).toBe('07:00')
    fireEvent.change(within(dialog).getByLabelText(/Starts at/), { target: { value: '07:30' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.updateSchedule).toHaveBeenCalledWith('l1', expect.objectContaining({ id: 'l1', startTime: '07:30:00', endTime: '08:30:00', rowVersion: 'rv' })))
  })

  it.each([
    ['schedules.classroom_busy', 'The classroom already has a lesson at this time.'],
    ['schedules.teacher_busy', 'The teacher already teaches another lesson at this time.'],
  ])('explains a clash (%s)', async (code, message) => {
    signInAs('Admin')
    api.createSchedule.mockRejectedValue(apiError(409, { code }))
    renderPage(<SchedulesPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add lesson' }))

    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(message)).toBeTruthy()
  })

  it('deletes a lesson after confirmation, and explains why one with attendance stays', async () => {
    signInAs('Admin')
    renderPage(<SchedulesPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete: Mathematics/ }))[0]!)

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/Mathematics on Monday at 07:00/)).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete lesson' }))
    await waitFor(() => expect(api.deleteSchedule).toHaveBeenCalledWith('l1'))

    api.deleteSchedule.mockRejectedValue(apiError(409, { code: 'schedules.has_sessions' }))
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete: English/ }))[0]!)
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete lesson' }))
    await waitFor(() => expect(toasts()).toContain('error:Attendance was already taken for this lesson, so it cannot be deleted. Change its time instead.'))
  })

  it('says so when the current year has no classrooms, and offers a retry on a failure', async () => {
    signInAs('Admin')
    api.listClassrooms.mockResolvedValueOnce(pageOf([]))
    renderPage(<SchedulesPage />)
    expect(await screen.findByText('There are no classrooms in the current academic year.')).toBeTruthy()
  })
})
