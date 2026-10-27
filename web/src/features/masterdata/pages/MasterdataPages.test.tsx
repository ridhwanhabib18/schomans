import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import { ClassroomsPage } from './ClassroomsPage'
import { SubjectsPage } from './SubjectsPage'
import { TeachersPage } from './TeachersPage'

const api = vi.hoisted(() => ({
  listTeachers: vi.fn(),
  createTeacher: vi.fn(),
  updateTeacher: vi.fn(),
  deleteTeacher: vi.fn(),
  listClassrooms: vi.fn(),
  createClassroom: vi.fn(),
  updateClassroom: vi.fn(),
  deleteClassroom: vi.fn(),
  listSubjects: vi.fn(),
  createSubject: vi.fn(),
  updateSubject: vi.fn(),
  deleteSubject: vi.fn(),
  listAcademicYears: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const teacher = (overrides: Record<string, unknown> = {}) => ({
  id: 't1',
  nip: '198001012005011001',
  fullName: 'Budi Santoso',
  email: 'budi@schomans.demo',
  hasAccount: true,
  isHomeroomTeacher: true,
  rowVersion: 'rv',
  ...overrides,
})

const classroom = (overrides: Record<string, unknown> = {}) => ({
  id: 'c1',
  name: 'X-A',
  academicYearId: 'y1',
  academicYearName: '2026/2027',
  homeroomTeacherId: 't1',
  homeroomTeacherName: 'Budi Santoso',
  studentCount: 30,
  rowVersion: 'rv',
  ...overrides,
})

const subject = (overrides: Record<string, unknown> = {}) => ({ id: 'sub1', name: 'Mathematics', minimumPassingScore: 70, rowVersion: 'rv', ...overrides })

const year = (overrides: Record<string, unknown> = {}) => ({
  id: 'y1',
  name: '2026/2027',
  isActive: true,
  startDate: '2026-07-13',
  endDate: '2027-06-26',
  terms: [],
  classroomCount: 2,
  rowVersion: 'rv',
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listTeachers.mockResolvedValue(pageOf([teacher(), teacher({ id: 't2', fullName: 'Ani Wijaya', nip: '198502022010012002', hasAccount: false, isHomeroomTeacher: false })]))
  api.listClassrooms.mockResolvedValue(pageOf([classroom(), classroom({ id: 'c2', name: 'X-B' })]))
  api.listSubjects.mockResolvedValue(pageOf([subject(), subject({ id: 'sub2', name: 'Physics', minimumPassingScore: 65.5 })]))
  api.listAcademicYears.mockResolvedValue(pageOf([year(), year({ id: 'y0', name: '2025/2026', isActive: false })]))
  for (const name of ['createTeacher', 'updateTeacher', 'deleteTeacher', 'createClassroom', 'updateClassroom', 'deleteClassroom', 'createSubject', 'updateSubject', 'deleteSubject'] as const) {
    api[name].mockResolvedValue(undefined)
  }
  useToastStore.setState({ toasts: [] })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('TeachersPage', () => {
  it('lists the teachers and marks the homeroom teachers and the accounts', async () => {
    signInAs('Principal')
    renderPage(<TeachersPage />)

    expect(await screen.findByText('Budi Santoso')).toBeTruthy()
    expect(screen.getByText('Ani Wijaya')).toBeTruthy()
    expect(screen.getByText('Homeroom teacher')).toBeTruthy()
    expect(screen.getByText('Signed in')).toBeTruthy()
    expect(screen.getByText('Not yet')).toBeTruthy()
  })

  it('lets only the admin change the list', async () => {
    signInAs('Principal')
    renderPage(<TeachersPage />)
    await screen.findByText('Budi Santoso')

    expect(screen.queryByRole('button', { name: 'Add teacher' })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Edit/ })).toBeNull()
  })

  it('adds a teacher', async () => {
    signInAs('Admin')
    renderPage(<TeachersPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add teacher' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add teacher' })
    fireEvent.change(within(dialog).getByLabelText(/Full name/), { target: { value: 'Citra Dewi' } })
    fireEvent.change(within(dialog).getByLabelText(/^Email/), { target: { value: 'citra@schomans.demo' } })
    fireEvent.change(within(dialog).getByLabelText(/NIP/), { target: { value: '199001012015012003' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createTeacher).toHaveBeenCalledWith({ fullName: 'Citra Dewi', email: 'citra@schomans.demo', nip: '199001012015012003' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(toasts()).toEqual(['success:Saved.'])
  })

  it('edits a teacher with the row version it loaded', async () => {
    signInAs('Admin')
    renderPage(<TeachersPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit/ }))[1]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit teacher' })
    fireEvent.change(within(dialog).getByLabelText(/Full name/), { target: { value: 'Ani Wijaya, M.Pd.' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.updateTeacher).toHaveBeenCalledWith('t2', expect.objectContaining({ id: 't2', fullName: 'Ani Wijaya, M.Pd.', rowVersion: 'rv' })))
  })

  it('explains a duplicate NIP', async () => {
    signInAs('Admin')
    api.createTeacher.mockRejectedValue(apiError(409, { code: 'teachers.nip_taken' }))
    renderPage(<TeachersPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add teacher' }))

    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Another teacher already has this NIP.')).toBeTruthy()
  })

  it('explains why a homeroom teacher cannot be deleted', async () => {
    signInAs('Admin')
    api.deleteTeacher.mockRejectedValue(apiError(409, { code: 'teachers.is_homeroom' }))
    renderPage(<TeachersPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete teacher' }))

    await waitFor(() => expect(toasts()).toEqual(['error:This teacher is the homeroom teacher of a classroom. Appoint another teacher there first.']))
  })

  it('deletes a teacher after confirmation', async () => {
    signInAs('Admin')
    renderPage(<TeachersPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[1]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete teacher' }))

    await waitFor(() => expect(api.deleteTeacher).toHaveBeenCalledWith('t2'))
  })
})

describe('ClassroomsPage', () => {
  it('lists the classrooms with their year, homeroom teacher and size', async () => {
    signInAs('Teacher')
    renderPage(<ClassroomsPage />)

    expect(await screen.findByText('X-A')).toBeTruthy()
    expect(screen.getAllByText('Budi Santoso').length).toBeGreaterThan(0)
    expect(screen.getAllByText('30').length).toBe(2)
    expect(screen.queryByRole('button', { name: 'Add classroom' })).toBeNull()
  })

  it('filters by academic year', async () => {
    signInAs('Admin')
    renderPage(<ClassroomsPage />)
    await screen.findByText('X-A')
    await waitFor(() => expect(screen.getByRole('option', { name: '2025/2026' })).toBeTruthy())

    fireEvent.change(screen.getByRole('combobox', { name: 'Academic year' }), { target: { value: 'y0' } })

    await waitFor(() => expect(api.listClassrooms).toHaveBeenLastCalledWith(expect.objectContaining({ AcademicYearId: 'y0', Page: 1 })))
  })

  it('creates a classroom in the current academic year unless another is chosen', async () => {
    signInAs('Admin')
    renderPage(<ClassroomsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add classroom' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add classroom' })
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'XI-C' } })
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Budi Santoso' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/Homeroom teacher/), { target: { value: 't1' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createClassroom).toHaveBeenCalledWith({ name: 'XI-C', academicYearId: 'y1', homeroomTeacherId: 't1' }))
  })

  it('does not let the academic year of a saved classroom change', async () => {
    signInAs('Admin')
    renderPage(<ClassroomsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit/ }))[0]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit classroom' })

    expect((within(dialog).getByLabelText(/Academic year/) as HTMLSelectElement).disabled).toBe(true)
  })

  it('explains a duplicate name and a classroom that still has students', async () => {
    signInAs('Admin')
    api.createClassroom.mockRejectedValue(apiError(409, { code: 'classrooms.name_taken' }))
    api.deleteClassroom.mockRejectedValue(apiError(409, { code: 'classrooms.has_students' }))
    renderPage(<ClassroomsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Add classroom' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('This academic year already has a classroom with this name.')).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete classroom' }))
    await waitFor(() => expect(toasts()).toEqual(['error:This classroom still has students. Move them to another classroom first.']))
  })
})

describe('SubjectsPage', () => {
  it('lists the subjects with the minimum passing score', async () => {
    signInAs('Teacher')
    renderPage(<SubjectsPage />)

    expect(await screen.findByText('Mathematics')).toBeTruthy()
    expect(screen.getByText('65.5')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Add subject' })).toBeNull()
  })

  it('sorts by the passing score when its header is clicked', async () => {
    signInAs('Admin')
    renderPage(<SubjectsPage />)
    await screen.findByText('Mathematics')

    fireEvent.click(screen.getByRole('button', { name: 'Minimum passing score' }))

    await waitFor(() => expect(api.listSubjects).toHaveBeenLastCalledWith(expect.objectContaining({ Sort: 'minimumPassingScore', Desc: false })))
  })

  it('creates a subject with a numeric score', async () => {
    signInAs('Admin')
    renderPage(<SubjectsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add subject' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add subject' })
    fireEvent.change(within(dialog).getByLabelText(/^Name/), { target: { value: 'Chemistry' } })
    fireEvent.change(within(dialog).getByLabelText(/Minimum passing score/), { target: { value: '72.5' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createSubject).toHaveBeenCalledWith({ name: 'Chemistry', minimumPassingScore: 72.5 }))
  })

  it('edits a subject starting from its current values', async () => {
    signInAs('Admin')
    renderPage(<SubjectsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit/ }))[0]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit subject' })
    expect((within(dialog).getByLabelText(/^Name/) as HTMLInputElement).value).toBe('Mathematics')
    fireEvent.change(within(dialog).getByLabelText(/Minimum passing score/), { target: { value: '75' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.updateSubject).toHaveBeenCalledWith('sub1', { id: 'sub1', name: 'Mathematics', minimumPassingScore: 75, rowVersion: 'rv' }))
  })

  it('shows the validation message of the score and a duplicate name', async () => {
    signInAs('Admin')
    api.createSubject.mockRejectedValueOnce(apiError(400, { errors: { MinimumPassingScore: ['The score must be between 0 and 100.'] } }))
    api.createSubject.mockRejectedValueOnce(apiError(409, { code: 'subjects.name_taken' }))
    renderPage(<SubjectsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add subject' }))
    const dialog = await screen.findByRole('dialog')

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('The score must be between 0 and 100.')).toBeTruthy()

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('A subject with this name already exists.')).toBeTruthy()
  })

  it('deletes a subject after confirmation', async () => {
    signInAs('Admin')
    renderPage(<SubjectsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[1]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete subject' }))

    await waitFor(() => expect(api.deleteSubject).toHaveBeenCalledWith('sub2'))
    await waitFor(() => expect(toasts()).toEqual(['success:Deleted.']))
  })
})
