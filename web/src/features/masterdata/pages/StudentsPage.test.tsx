import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import { StudentsPage } from './StudentsPage'

const api = vi.hoisted(() => ({
  listStudents: vi.fn(),
  createStudent: vi.fn(),
  updateStudent: vi.fn(),
  deleteStudent: vi.fn(),
  importStudents: vi.fn(),
  listAcademicYears: vi.fn(),
  listClassrooms: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const student = (overrides: Record<string, unknown> = {}) => ({
  id: 's1',
  nisn: '0012345678',
  fullName: 'Rina Putri',
  email: 'rina@schomans.demo',
  dateOfBirth: '2010-05-17',
  classroomId: 'c1',
  classroomName: 'X-A',
  status: 'Active',
  hasAccount: false,
  rowVersion: 'rv1',
  ...overrides,
})

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listStudents.mockResolvedValue(pageOf([student(), student({ id: 's2', fullName: 'Budi Hartono', nisn: '0087654321', classroomName: 'X-B' })]))
  api.listAcademicYears.mockResolvedValue(pageOf([{ id: 'y1', name: '2026/2027', isActive: true, terms: [], startDate: '2026-07-13', endDate: '2027-06-26', classroomCount: 2, rowVersion: 'r' }]))
  api.listClassrooms.mockResolvedValue(pageOf([{ id: 'c1', name: 'X-A', studentCount: 30 }, { id: 'c2', name: 'X-B', studentCount: 28 }]))
  api.createStudent.mockResolvedValue({ id: 's3' })
  api.updateStudent.mockResolvedValue(undefined)
  api.deleteStudent.mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

describe('StudentsPage', () => {
  it('lists the students with their classroom and status', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText('Budi Hartono')).toBeTruthy()
    expect(screen.getByText('0087654321')).toBeTruthy()
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0)
  })

  it('asks the API for active students first, sorted by name', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)

    await screen.findByText('Rina Putri')
    expect(api.listStudents).toHaveBeenCalledWith(expect.objectContaining({ Page: 1, PageSize: 20, Status: 'Active', Sort: 'name', Desc: false }))
  })

  it('gives the admin the buttons to add, import, edit and delete', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)

    await screen.findByText('Rina Putri')
    expect(screen.getByRole('button', { name: 'Add student' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Import from Excel' })).toBeTruthy()
    expect(screen.getAllByRole('button', { name: /^Edit/ })).toHaveLength(2)
    expect(screen.getAllByRole('button', { name: /^Delete/ })).toHaveLength(2)
  })

  it.each(['Teacher', 'Principal'])('shows a %s the list without any way to change it', async (role) => {
    signInAs(role)
    renderPage(<StudentsPage />)

    await screen.findByText('Rina Putri')
    expect(screen.queryByRole('button', { name: 'Add student' })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Edit/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^Delete/ })).toBeNull()
  })

  it('searches after the typing has paused and goes back to the first page', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)
    await screen.findByText('Rina Putri')

    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'rina' } })

    await waitFor(() => expect(api.listStudents).toHaveBeenLastCalledWith(expect.objectContaining({ Search: 'rina', Page: 1 })))
  })

  it('shows the filtered empty state when nothing matches', async () => {
    signInAs('Admin')
    api.listStudents.mockResolvedValue(pageOf([]))
    renderPage(<StudentsPage />)

    expect(await screen.findByText('No students found')).toBeTruthy()
  })

  it('offers a retry when the list cannot be loaded', async () => {
    signInAs('Admin')
    api.listStudents.mockRejectedValueOnce(apiError(500)).mockResolvedValue(pageOf([student()]))
    renderPage(<StudentsPage />)

    expect(await screen.findByText('The data could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
  })

  it('adds a student with the fields of the form, then closes the dialog and confirms', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add student' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add student' })
    fireEvent.change(within(dialog).getByLabelText(/Full name/), { target: { value: 'Dewi Lestari' } })
    fireEvent.change(within(dialog).getByLabelText(/^Email/), { target: { value: 'dewi@schomans.demo' } })
    fireEvent.change(within(dialog).getByLabelText(/NISN/), { target: { value: '0099887766' } })
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'X-A' })).toBeTruthy())
    fireEvent.change(within(dialog).getByLabelText(/Classroom/), { target: { value: 'c1' } })
    fireEvent.change(within(dialog).getByLabelText(/Date of birth/), { target: { value: '2010-01-02' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.createStudent).toHaveBeenCalledTimes(1))
    expect(api.createStudent).toHaveBeenCalledWith({
      fullName: 'Dewi Lestari',
      email: 'dewi@schomans.demo',
      nisn: '0099887766',
      classroomId: 'c1',
      dateOfBirth: '2010-01-02',
    })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(toasts()).toEqual(['success:Saved.'])
  })

  it('shows the field messages of a validation failure and keeps the dialog open', async () => {
    signInAs('Admin')
    api.createStudent.mockRejectedValue(apiError(400, { errors: { Nisn: ['NISN must be exactly 10 digits.'] } }))
    renderPage(<StudentsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add student' }))

    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('NISN must be exactly 10 digits.')).toBeTruthy()
    expect(screen.getByRole('dialog')).toBeTruthy()
  })

  it('explains a business error in the words of the feature', async () => {
    signInAs('Admin')
    api.createStudent.mockRejectedValue(apiError(409, { code: 'students.nisn_taken', title: 'Conflict' }))
    renderPage(<StudentsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Add student' }))

    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Another student already has this NISN.')).toBeTruthy()
  })

  it('says somebody else changed the student when the row version is stale', async () => {
    signInAs('Admin')
    api.updateStudent.mockRejectedValue(apiError(409, { code: 'concurrency.conflict' }))
    renderPage(<StudentsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit/ }))[0]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit student' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText(/Somebody else changed this record/)).toBeTruthy()
    expect(api.updateStudent).toHaveBeenCalledWith('s1', expect.objectContaining({ id: 's1', rowVersion: 'rv1' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reload' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('locks the name and email of a student who already signed in', async () => {
    signInAs('Admin')
    api.listStudents.mockResolvedValue(pageOf([student({ hasAccount: true })]))
    renderPage(<StudentsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Edit/ }))[0]!)

    const dialog = await screen.findByRole('dialog', { name: 'Edit student' })

    expect((within(dialog).getByLabelText(/Full name/) as HTMLInputElement).disabled).toBe(true)
    expect((within(dialog).getByLabelText(/^Email/) as HTMLInputElement).disabled).toBe(true)
    expect((within(dialog).getByLabelText(/NISN/) as HTMLInputElement).disabled).toBe(false)
  })

  it('deletes a student only after the admin confirms', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/Rina Putri is removed from the lists/)).toBeTruthy()
    expect(api.deleteStudent).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete student' }))

    await waitFor(() => expect(api.deleteStudent).toHaveBeenCalledWith('s1'))
    await waitFor(() => expect(toasts()).toEqual(['success:Deleted.']))
  })

  it('does not delete when the admin cancels', async () => {
    signInAs('Admin')
    renderPage(<StudentsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)

    fireEvent.click(await screen.findByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
    expect(api.deleteStudent).not.toHaveBeenCalled()
  })

  it('tells the admin when a delete fails', async () => {
    signInAs('Admin')
    api.deleteStudent.mockRejectedValue(apiError(500, { title: 'Boom' }))
    renderPage(<StudentsPage />)
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete student' }))

    await waitFor(() => expect(toasts()).toEqual(['error:Boom']))
  })

  describe('import', () => {
    const xlsx = (name = 'students.xlsx') => new File(['x'], name, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })

    async function openImport() {
      signInAs('Admin')
      renderPage(<StudentsPage />)
      fireEvent.click(await screen.findByRole('button', { name: 'Import from Excel' }))
      return screen.findByRole('dialog', { name: 'Import students from Excel' })
    }

    it('asks for a file before it sends anything', async () => {
      const dialog = await openImport()

      fireEvent.click(within(dialog).getByRole('button', { name: 'Import' }))

      expect(await within(dialog).findByText('Choose a file first.')).toBeTruthy()
      expect(api.importStudents).not.toHaveBeenCalled()
    })

    it('refuses a file that is not .xlsx', async () => {
      const dialog = await openImport()

      fireEvent.change(within(dialog).getByLabelText(/Excel file/), { target: { files: [xlsx('students.csv')] } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Import' }))

      expect(await within(dialog).findByText('Choose an .xlsx file.')).toBeTruthy()
      expect(api.importStudents).not.toHaveBeenCalled()
    })

    it('reports how many rows were imported when all of them worked', async () => {
      api.importStudents.mockResolvedValue({ totalRows: 3, created: 3, failed: [] })
      const dialog = await openImport()

      fireEvent.change(within(dialog).getByLabelText(/Excel file/), { target: { files: [xlsx()] } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Import' }))

      const summary = await within(dialog).findByRole('status')
      expect(summary.textContent).toContain('3 of 3 rows were imported.')
      expect(summary.textContent).toContain('Every row was imported.')
    })

    it('lists the rows that failed with the reason, and lets the admin start over', async () => {
      api.importStudents.mockResolvedValue({
        totalRows: 3,
        created: 1,
        failed: [
          { row: 3, message: 'NISN must be exactly 10 digits.' },
          { row: 4, message: 'Classroom "Z-9" does not exist.' },
        ],
      })
      const dialog = await openImport()

      fireEvent.change(within(dialog).getByLabelText(/Excel file/), { target: { files: [xlsx()] } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Import' }))

      expect(await within(dialog).findByText('2 row(s) were not imported')).toBeTruthy()
      expect(within(dialog).getByRole('table', { name: 'Rows that were not imported' })).toBeTruthy()
      expect(within(dialog).getByText('Classroom "Z-9" does not exist.')).toBeTruthy()

      fireEvent.click(within(dialog).getByRole('button', { name: 'Import another file' }))
      expect(await within(dialog).findByLabelText(/Excel file/)).toBeTruthy()
    })

    it('shows what the server says is wrong with the file itself', async () => {
      api.importStudents.mockRejectedValue(apiError(400, { title: 'The file has more than 2000 rows.' }))
      const dialog = await openImport()

      fireEvent.change(within(dialog).getByLabelText(/Excel file/), { target: { files: [xlsx()] } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Import' }))

      expect(await within(dialog).findByText(/The file has more than 2000 rows\./)).toBeTruthy()
    })
  })
})
