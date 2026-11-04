import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderWithQuery } from '@/test/utils'
import type { Analytics } from '../api/analyticsTypes'
import { DashboardPage } from './DashboardPage'

// The principal's card of requests links to the approvals, so the page needs a router around it.
const renderDashboard = () =>
  renderWithQuery(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>,
  )

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

    renderDashboard()

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

    renderDashboard()

    expect((await screen.findByRole('heading', { level: 1 })).textContent).toBe(title)
    expect(screen.getByText('Welcome, Rina Putri')).toBeTruthy()
  })

  it.each(['Admin', 'Applicant'])('shows the empty dashboard for %s, which has no charts, and asks for no analytics', async (role) => {
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderDashboard()

    expect(await screen.findByText('Nothing to show yet')).toBeTruthy()
    expect(http.customInstance).not.toHaveBeenCalled()
  })

  it('shows an error with a retry button when the API call fails, and recovers', async () => {
    getDashboard.mockRejectedValueOnce(new Error('boom'))
    getDashboard.mockResolvedValueOnce({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })

    renderDashboard()

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

    renderDashboard()

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

    renderDashboard()

    const table = await screen.findByRole('table', { name: 'Attendance rate per class and month' })
    expect(table.className).not.toContain('sr-only')
    expect(table.parentElement?.className).toContain('sr-only')
  })

  it('tells the principal when there is no data to draw yet', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockResolvedValue(analytics({ principal: { attendanceTrend: [], subjectAverages: [] } }))

    renderDashboard()

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

    renderDashboard()

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

  it('shows the total, the ten most urgent students first, and lets the teacher show all or fewer', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    // 25 students: one reason each with falling attendance, plus one with three reasons that is listed last in the data.
    const flagged = [
      ...Array.from({ length: 24 }, (_, i) => ({
        studentId: `s${i}`,
        studentName: `Student ${String(i).padStart(2, '0')}`,
        classroomName: 'Class A',
        attendanceRate: 50 + i,
        lowAttendance: true,
        belowPassingSubjects: [] as string[],
      })),
      { studentId: 'urgent', studentName: 'Zed Urgent', classroomName: 'Class B', attendanceRate: 90, lowAttendance: false, belowPassingSubjects: ['Maths', 'Art', 'Music'] },
    ]
    http.customInstance.mockResolvedValue(analytics({ role: 'Teacher', teacher: { attendanceThreshold: 80, flagged } }))

    renderDashboard()

    const table = await screen.findByRole('table', { name: 'Students who need attention' })
    expect(screen.getByTestId('attention-total').textContent).toContain('25 students in total')
    expect(screen.getByTestId('attention-total').textContent).toContain('Showing 10 of 25, the most urgent first.')
    const names = () => within(table).getAllByRole('row').slice(1).map((row) => within(row).getAllByRole('cell')[0]!.textContent)
    expect(names()).toHaveLength(10)
    // Most reasons first, then the lowest attendance.
    expect(names().slice(0, 3)).toEqual(['Zed Urgent', 'Student 00', 'Student 01'])

    const toggle = screen.getByRole('button', { name: 'Show all (25)' })
    expect(toggle.getAttribute('aria-expanded')).toBe('false')
    fireEvent.click(toggle)

    expect(names()).toHaveLength(25)
    expect(screen.getByRole('button', { name: 'Show fewer' }).getAttribute('aria-expanded')).toBe('true')
    fireEvent.click(screen.getByRole('button', { name: 'Show fewer' }))
    expect(names()).toHaveLength(10)
  })

  it('has no toggle when ten students or fewer need attention', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    const flagged = Array.from({ length: 10 }, (_, i) => ({
      studentId: `s${i}`,
      studentName: `Student ${i}`,
      classroomName: 'Class A',
      attendanceRate: 60,
      lowAttendance: true,
      belowPassingSubjects: [] as string[],
    }))
    http.customInstance.mockResolvedValue(analytics({ role: 'Teacher', teacher: { attendanceThreshold: 80, flagged } }))

    renderDashboard()

    await screen.findByRole('table', { name: 'Students who need attention' })
    expect(screen.getByTestId('attention-total').textContent).toBe('10 students in total')
    expect(screen.queryByRole('button', { name: /Show all|Show fewer/ })).toBeNull()
  })

  it('says so when no student needs attention', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    http.customInstance.mockResolvedValue(analytics({ role: 'Teacher', teacher: { attendanceThreshold: 80, flagged: [] } }))

    renderDashboard()

    expect(await screen.findByText('No student needs attention right now.')).toBeTruthy()
  })

  it('offers a parent the chatbot of the school, and nobody else', async () => {
    getDashboard.mockResolvedValue({ role: 'Parent', fullName: 'Ibu Putri', sections: [] })
    const parent = renderDashboard()
    expect(await screen.findByRole('heading', { name: 'Ask about the school' })).toBeTruthy()
    parent.unmount()

    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Pak Budi', sections: [] })
    renderDashboard()
    await screen.findByRole('heading', { level: 1 })
    expect(screen.queryByRole('heading', { name: 'Ask about the school' })).toBeNull()
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

    renderDashboard()

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

    renderDashboard()

    expect((await screen.findByRole('alert')).textContent).toContain('The charts could not be loaded.')
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Student dashboard')
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { name: 'Progress of Rina Putri' })).toBeTruthy()
  })

  it('shows the principal how many requests wait for a decision, with a link to the approvals', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockImplementation(async ({ url }: { url: string }) =>
      url === '/api/v1/requests/inbox' ? { items: [], page: 1, pageSize: 1, totalCount: 3 } : analytics({ principal: { attendanceTrend: [], subjectAverages: [] } }),
    )

    renderDashboard()

    expect(await screen.findByRole('heading', { name: 'Requests awaiting approval' })).toBeTruthy()
    expect(await screen.findByText('3 requests')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Open the approvals' }).getAttribute('href')).toBe('/requests/inbox')
    expect(http.customInstance).toHaveBeenCalledWith({ url: '/api/v1/requests/inbox', method: 'GET', params: { page: 1, pageSize: 1 } })
  })

  it('keeps the page and says so when the requests count fails', async () => {
    getDashboard.mockResolvedValue({ role: 'Principal', fullName: 'Hendra Wijaya', sections: [] })
    http.customInstance.mockImplementation(async ({ url }: { url: string }) => {
      if (url === '/api/v1/requests/inbox') throw apiError(500)
      return analytics({ principal: { attendanceTrend: [], subjectAverages: [] } })
    })

    renderDashboard()

    expect((await screen.findByText('The requests could not be counted.')).textContent).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Attendance per class' })).toBeTruthy()
  })

  it('does not ask a teacher or a family for the requests count', async () => {
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })
    http.customInstance.mockResolvedValue(analytics({ role: 'Teacher', teacher: { attendanceThreshold: 80, flagged: [] } }))

    renderDashboard()

    await screen.findByText('No student needs attention right now.')
    expect(http.customInstance.mock.calls.some(([call]) => call.url === '/api/v1/requests/inbox')).toBe(false)
  })
})
