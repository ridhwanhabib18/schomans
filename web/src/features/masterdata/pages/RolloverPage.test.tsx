import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { RolloverPage } from './RolloverPage'

const api = vi.hoisted(() => ({
  listAcademicYears: vi.fn(),
  listClassrooms: vi.fn(),
  listTeachers: vi.fn(),
  rolloverAcademicYear: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const classroom = (id: string, name: string, studentCount: number, teacher = 't1') => ({
  id,
  name,
  academicYearId: 'y1',
  academicYearName: '2026/2027',
  homeroomTeacherId: teacher,
  homeroomTeacherName: 'Budi',
  studentCount,
  rowVersion: 'rv',
})

const currentYear = { id: 'y1', name: '2026/2027', isActive: true, startDate: '2026-07-13', endDate: '2027-06-26', terms: [], classroomCount: 2, rowVersion: 'rv' }

function renderWizard() {
  return renderRoutes(
    [
      { path: '/', element: <RolloverPage /> },
      { path: '/manage/academic-years', element: <p>Academic years list</p> },
    ],
    '/',
  )
}

function fillYearStep() {
  fireEvent.change(screen.getByLabelText(/^Name/), { target: { value: '2027/2028' } })
  fireEvent.change(screen.getByLabelText(/Starts on/), { target: { value: '2027-07-12' } })
  fireEvent.change(screen.getByLabelText(/Ends on/), { target: { value: '2028-06-24' } })
  fireEvent.change(screen.getByLabelText(/Term name/), { target: { value: 'Odd' } })
  fireEvent.change(screen.getByLabelText(/Term starts on/), { target: { value: '2027-07-12' } })
  fireEvent.change(screen.getByLabelText(/Term ends on/), { target: { value: '2027-12-18' } })
}

async function reachClassroomStep() {
  renderWizard()
  await screen.findByRole('heading', { name: 'The new academic year' })
  fillYearStep()
  fireEvent.click(screen.getByRole('button', { name: 'Next' }))
  await screen.findByRole('heading', { name: 'What happens to each classroom?' })
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  signInAs('Admin')
  api.listAcademicYears.mockResolvedValue(pageOf([currentYear]))
  api.listClassrooms.mockResolvedValue(pageOf([classroom('c1', 'X-A', 30), classroom('c2', 'XII-A', 25)]))
  api.listTeachers.mockResolvedValue(pageOf([{ id: 't1', fullName: 'Budi Santoso' }, { id: 't2', fullName: 'Ani Wijaya' }]))
  api.rolloverAcademicYear.mockResolvedValue({ academicYearId: 'y2', movedStudents: 30, graduatedStudents: 25, createdClassrooms: 1 })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('RolloverPage', () => {
  it('starts with the new year and does not move on while it is incomplete', async () => {
    renderWizard()
    await screen.findByRole('heading', { name: 'The new academic year' })

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    const problems = await screen.findByRole('alert')
    expect(problems.textContent).toContain('Enter a name for the new academic year.')
    expect(problems.textContent).toContain('The year must end after it starts.')
    expect(problems.textContent).toContain('Add between 1 and 4 terms')
    expect(screen.queryByRole('heading', { name: 'What happens to each classroom?' })).toBeNull()
  })

  it('says there is nothing to start from when no year is current', async () => {
    api.listAcademicYears.mockResolvedValue(pageOf([{ ...currentYear, isActive: false }]))
    renderWizard()

    expect(await screen.findByText('There is no current academic year to start from.')).toBeTruthy()
  })

  it('offers a retry when the data cannot be loaded', async () => {
    api.listAcademicYears.mockRejectedValueOnce(apiError(500)).mockResolvedValue(pageOf([currentYear]))
    renderWizard()

    expect(await screen.findByText('The data could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { name: 'The new academic year' })).toBeTruthy()
  })

  it('lists every classroom of the current year with its students on the second step', async () => {
    await reachClassroomStep()

    const table = screen.getByRole('table', { name: 'Classrooms of the current academic year' })
    expect(within(table).getByRole('rowheader', { name: 'X-A' })).toBeTruthy()
    expect(within(table).getByRole('rowheader', { name: 'XII-A' })).toBeTruthy()
    expect(within(table).getByText('30')).toBeTruthy()
    expect(screen.getByText(/Every classroom of 2026\/2027 needs a decision/)).toBeTruthy()
  })

  it('asks for a name and a teacher for each classroom that moves up', async () => {
    await reachClassroomStep()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    const problems = await screen.findByRole('alert')
    expect(problems.textContent).toContain('Name the new classroom of X-A.')
    expect(problems.textContent).toContain('Name the new classroom of XII-A.')
  })

  it('does not ask anything of a classroom that graduates, and disables its fields', async () => {
    await reachClassroomStep()

    fireEvent.change(screen.getByLabelText('What happens: XII-A'), { target: { value: 'graduate' } })

    expect((screen.getByLabelText('Name of the new classroom: XII-A') as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByLabelText('Homeroom teacher: XII-A') as HTMLSelectElement).disabled).toBe(true)
  })

  it('asks for one homeroom teacher when two classrooms move into the same new classroom', async () => {
    await reachClassroomStep()

    fireEvent.change(screen.getByLabelText('Name of the new classroom: X-A'), { target: { value: 'XI-A' } })
    fireEvent.change(screen.getByLabelText('Name of the new classroom: XII-A'), { target: { value: 'XI-A' } })
    fireEvent.change(screen.getByLabelText('Homeroom teacher: XII-A'), { target: { value: 't2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect((await screen.findByRole('alert')).textContent).toContain('The classrooms that move into XI-A need the same homeroom teacher.')
  })

  it('reviews the plan, goes back to change it, and sends nothing before the last click', async () => {
    await reachClassroomStep()
    fireEvent.change(screen.getByLabelText('Name of the new classroom: X-A'), { target: { value: 'XI-A' } })
    fireEvent.change(screen.getByLabelText('What happens: XII-A'), { target: { value: 'graduate' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    await screen.findByRole('heading', { name: 'Check before you start' })
    expect(screen.getByText('30 student(s) move up, 25 graduate, and 1 new classroom(s) are created in 2027/2028.')).toBeTruthy()
    expect(screen.getByText('X-A → XI-A')).toBeTruthy()
    expect(screen.getByText('XII-A graduates')).toBeTruthy()
    expect(api.rolloverAcademicYear).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(await screen.findByRole('heading', { name: 'What happens to each classroom?' })).toBeTruthy()
    expect((screen.getByLabelText('Name of the new classroom: X-A') as HTMLInputElement).value).toBe('XI-A')
  })

  it('sends the whole plan in one request and shows the result', async () => {
    await reachClassroomStep()
    fireEvent.change(screen.getByLabelText('Name of the new classroom: X-A'), { target: { value: ' XI-A ' } })
    fireEvent.change(screen.getByLabelText('What happens: XII-A'), { target: { value: 'graduate' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByRole('heading', { name: 'Check before you start' })

    fireEvent.click(screen.getByRole('button', { name: 'Start the new academic year' }))

    await waitFor(() => expect(api.rolloverAcademicYear).toHaveBeenCalledTimes(1))
    expect(api.rolloverAcademicYear).toHaveBeenCalledWith({
      name: '2027/2028',
      startDate: '2027-07-12',
      endDate: '2028-06-24',
      terms: [{ id: null, name: 'Odd', startDate: '2027-07-12', endDate: '2027-12-18' }],
      mappings: [
        { fromClassroomId: 'c1', graduate: false, toClassroomName: 'XI-A', homeroomTeacherId: 't1' },
        { fromClassroomId: 'c2', graduate: true, toClassroomName: null, homeroomTeacherId: null },
      ],
    })
    expect(await screen.findByRole('heading', { name: 'The new academic year has started' })).toBeTruthy()
    expect(screen.getByText('30 student(s) moved up, 25 graduated, 1 classroom(s) created.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'See the classrooms' }).getAttribute('href')).toBe('/manage/classrooms')
  })

  it('shows the server error in words and keeps the plan when the rollover fails', async () => {
    api.rolloverAcademicYear.mockRejectedValue(apiError(409, { code: 'academic_years.rollover_mapping_incomplete' }))
    await reachClassroomStep()
    fireEvent.change(screen.getByLabelText('Name of the new classroom: X-A'), { target: { value: 'XI-A' } })
    fireEvent.change(screen.getByLabelText('What happens: XII-A'), { target: { value: 'graduate' } })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByRole('heading', { name: 'Check before you start' })

    fireEvent.click(screen.getByRole('button', { name: 'Start the new academic year' }))

    expect(await screen.findByText('Every classroom of the current year must be mapped exactly once. Reload the page and try again.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Start the new academic year' })).toBeTruthy()
  })

  it('goes back to the list of academic years', async () => {
    renderWizard()
    await screen.findByRole('heading', { name: 'The new academic year' })

    fireEvent.click(screen.getAllByRole('link', { name: /Academic years/ })[0]!)

    expect(await screen.findByText('Academic years list')).toBeTruthy()
  })
})
