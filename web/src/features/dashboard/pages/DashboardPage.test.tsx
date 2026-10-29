import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderWithQuery } from '@/test/utils'
import type { Analytics } from '../api/analyticsTypes'
import { DashboardPage } from './DashboardPage'

const { getDashboard } = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('@/api/generated/schomans', () => ({ getDashboard }))

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const months = ['2026-08', '2026-09', '2026-10']

const analytics = (overrides: Partial<Analytics> = {}): Analytics => ({
  role: 'Principal',
  principal: null,
  teacher: null,
  children: [],
  ...overrides,
})

beforeEach(() => {
  getDashboard.mockReset()
  http.customInstance.mockReset()
  http.customInstance.mockResolvedValue(analytics())
})

describe('DashboardPage', () => {
  it('shows a skeleton while loading', () => {
    getDashboard.mockReturnValue(new Promise(() => {}))

    renderWithQuery(<DashboardPage />)

    expect(screen.getByRole('status')).toHaveProperty('ariaBusy', 'true')
  })

  it.each([
    ['Principal', 'Principal dashboard'],
    ['Teacher', 'Teacher dashboard'],
    ['Student', 'Student dashboard'],
    ['Parent', 'Parent dashboard'],
    ['Admin', 'Admin dashboard'],
    ['Applicant', 'Applicant dashboard'],
  ])('shows the title and the welcome line of the %s dashboard', async (role, title) => {
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderWithQuery(<DashboardPage />)

    expect((await screen.findByRole('heading', { level: 1 })).textContent).toBe(title)
    expect(screen.getByText('Welcome, Rina Putri')).toBeTruthy()
  })

  it.each(['Admin', 'Applicant'])('shows the empty dashboard for %s, which has no charts, and asks for no analytics', async (role) => {
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderWithQuery(<DashboardPage />)

    expect(await screen.findByText('Nothing to show yet')).toBeTruthy()
    expect(http.customInstance).not.toHaveBeenCalled()
  })

  it('shows an error with a retry button when the API call fails, and recovers', async () => {
    getDashboard.mockRejectedValueOnce(new Error('boom'))
    getDashboard.mockResolvedValueOnce({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })

    renderWithQuery(<DashboardPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The dashboard could not be loaded.')
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Teacher dashboard'))
  })

  it('shows the principal the attendance trend per class and the average grade per subject', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockResolvedValue(
      analytics({
        principal: {
          attendanceTrend: [
            { classroomId: 'c1', classroomName: 'Class A', months: months.map((month, i) => ({ month, rate: 90 + i })) },
            { classroomId: 'c2', classroomName: 'Class B', months: months.map((month) => ({ month, rate: null })) },
          ],
          subjectAverages: [
            { subjectId: 'math', subjectName: 'Mathematics', average: 82.5, students: 30 },
            { subjectId: 'sci', subjectName: 'Natural Sciences', average: 76, students: 30 },
          ],
        },
      }),
    )

    renderWithQuery(<DashboardPage />)

    expect(await screen.findByRole('heading', { name: 'Attendance per class' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Average grade per subject' })).toBeTruthy()
    expect(screen.getByRole('img', { name: /monthly attendance rate per class/ })).toBeTruthy()

    const trend = screen.getByRole('table', { name: 'Attendance rate per class and month' })
    expect(within(trend).getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Month', 'Class A', 'Class B'])
    expect(within(trend).getByRole('row', { name: /October 2026/ }).textContent).toBe('October 202692—')

    const averages = screen.getByRole('table', { name: 'Average final grade per subject' })
    expect(within(averages).getByRole('row', { name: /Mathematics/ }).textContent).toBe('Mathematics82.530')
  })

  it('hides the reader tables with a wrapper, so a wide table cannot widen the page on a phone', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockResolvedValue(
      analytics({ principal: { attendanceTrend: [{ classroomId: 'c1', classroomName: 'Class A', months: months.map((month) => ({ month, rate: 90 })) }], subjectAverages: [] } }),
    )

    renderWithQuery(<DashboardPage />)

    const table = await screen.findByRole('table', { name: 'Attendance rate per class and month' })
    expect(table.className).not.toContain('sr-only')
    expect(table.parentElement?.className).toContain('sr-only')
  })

  it('tells the principal when there is no data to draw yet', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockResolvedValue(analytics({ principal: { attendanceTrend: [], subjectAverages: [] } }))

    renderWithQuery(<DashboardPage />)

    expect(await screen.findByText('There are no attendance records yet.')).toBeTruthy()
    expect(screen.getByText('There are no grades in the current term yet.')).toBeTruthy()
  })

  it('lists the students who need attention to a teacher, with the reasons', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    http.customInstance.mockResolvedValue(
      analytics({
        role: 'Teacher',
        teacher: {
          attendanceThreshold: 80,
          flagged: [
            { studentId: 's1', studentName: 'Dimas Pratama', classroomName: 'Class A', attendanceRate: 70, lowAttendance: true, belowPassingSubjects: ['Mathematics'] },
            { studentId: 's2', studentName: 'Sari Lestari', classroomName: 'Class B', attendanceRate: null, lowAttendance: false, belowPassingSubjects: ['Natural Sciences', 'Art'] },
          ],
        },
      }),
    )

    renderWithQuery(<DashboardPage />)

    const table = await screen.findByRole('table', { name: 'Students who need attention' })
    expect(screen.getByText('Attendance below 80% or a final grade below the minimum passing score, in the current term.')).toBeTruthy()
    const first = within(table).getByRole('row', { name: /Dimas Pratama/ })
    expect(within(first).getByText('70%')).toBeTruthy()
    expect(within(first).getByText('Low attendance')).toBeTruthy()
    expect(within(first).getByText('Below minimum: Mathematics')).toBeTruthy()
    const second = within(table).getByRole('row', { name: /Sari Lestari/ })
    expect(within(second).getByText('No records')).toBeTruthy()
    expect(within(second).getByText('Below minimum: Natural Sciences, Art')).toBeTruthy()
    expect(within(second).queryByText('Low attendance')).toBeNull()
  })

  it('says so when no student needs attention', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    http.customInstance.mockResolvedValue(analytics({ role: 'Teacher', teacher: { attendanceThreshold: 80, flagged: [] } }))

    renderWithQuery(<DashboardPage />)

    expect(await screen.findByText('No student needs attention right now.')).toBeTruthy()
  })

  it('shows a parent the monthly grades and attendance of each child', async () => {
    getDashboard.mockResolvedValue({ role: 'Parent', fullName: 'Ibu Putri', sections: [] })
    http.customInstance.mockResolvedValue(
      analytics({
        role: 'Parent',
        children: [
          {
            studentId: 's1',
            studentName: 'Rina Putri',
            months: [
              { month: '2026-09', averageGrade: 84, attendanceRate: 95 },
              { month: '2026-10', averageGrade: null, attendanceRate: 100 },
            ],
          },
          { studentId: 's2', studentName: 'Adi Putra', months: [{ month: '2026-10', averageGrade: null, attendanceRate: null }] },
        ],
      }),
    )

    renderWithQuery(<DashboardPage />)

    expect(await screen.findByRole('heading', { name: 'Progress of Rina Putri' })).toBeTruthy()
    const table = screen.getByRole('table', { name: 'Average grade and attendance per month for Rina Putri' })
    expect(within(table).getByRole('row', { name: /September 2026/ }).textContent).toBe('September 20268495')
    expect(within(table).getByRole('row', { name: /October 2026/ }).textContent).toBe('October 2026—100')
    expect(screen.getByRole('heading', { name: 'Progress of Adi Putra' })).toBeTruthy()
    expect(screen.getByText('There is no data for this child yet.')).toBeTruthy()
  })

  it('keeps the page and shows a retry when only the charts fail to load', async () => {
    getDashboard.mockResolvedValue({ role: 'Student', fullName: 'Rina Putri', sections: [] })
    http.customInstance.mockRejectedValueOnce(apiError(500))
    http.customInstance.mockResolvedValueOnce(
      analytics({ role: 'Student', children: [{ studentId: 's1', studentName: 'Rina Putri', months: [{ month: '2026-10', averageGrade: 80, attendanceRate: 90 }] }] }),
    )

    renderWithQuery(<DashboardPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The charts could not be loaded.')
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Student dashboard')
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { name: 'Progress of Rina Putri' })).toBeTruthy()
  })
})
