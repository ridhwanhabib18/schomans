import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useChildStore } from '@/stores/useChildStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { StudentGrades } from '../api/reportCards'
import { ChildGradesPage, MyGradesPage } from './GradesPages'

const api = vi.hoisted(() => ({ getMyAttendance: vi.fn(), listAcademicYears: vi.fn(), listMyChildren: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const open = vi.hoisted(() => ({ tab: vi.fn() }))
vi.mock('@/shared/lib/download', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/download')>()), openInNewTab: open.tab }))

const grades = (overrides: Partial<StudentGrades> = {}): StudentGrades => ({
  studentId: 's1',
  termId: 't1',
  publishedAt: '2026-12-18T03:00:00Z',
  hasReportCard: true,
  subjects: [
    {
      subjectId: 'math',
      subjectName: 'Mathematics',
      minimumPassingScore: 75,
      components: [
        { component: 'Assignment', average: 88, count: 2 },
        { component: 'Quiz', average: 80, count: 1 },
        { component: 'Midterm', average: 90, count: 1 },
        { component: 'Final', average: null, count: 0 },
      ],
      finalGrade: 86.5,
      band: 'B',
      isPassing: true,
      description: 'Understands fractions well.',
    },
    {
      subjectId: 'sci',
      subjectName: 'Natural Sciences',
      minimumPassingScore: 75,
      components: [
        { component: 'Assignment', average: 60, count: 1 },
        { component: 'Quiz', average: null, count: 0 },
        { component: 'Midterm', average: null, count: 0 },
        { component: 'Final', average: null, count: 0 },
      ],
      finalGrade: 60,
      band: 'D',
      isPassing: false,
      description: null,
    },
  ],
  ...overrides,
})

const calls = (url: string) => http.customInstance.mock.calls.filter(([c]) => c.url === url).map(([c]) => c)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  open.tab.mockReset()
  useChildStore.setState({ childId: null })
  api.getMyAttendance.mockResolvedValue({ studentId: 's1' })
  api.listMyChildren.mockResolvedValue([{ studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' }])
  api.listAcademicYears.mockResolvedValue(
    pageOf([
      {
        id: 'y1',
        name: '2026/2027',
        startDate: '2026-07-01',
        endDate: '2027-06-30',
        isActive: true,
        classroomCount: 1,
        rowVersion: 'rv',
        terms: [{ id: 't1', name: 'Term 1', startDate: '2026-07-01', endDate: '2026-12-20', isActive: true }],
      },
    ]),
  )
})

describe('MyGradesPage', () => {
  beforeEach(() => signInAs('Student', 'Rina Putri'))

  it('shows the published grades of the current term with the band, result and report texts', async () => {
    http.customInstance.mockResolvedValue(grades())
    renderPage(<MyGradesPage />)

    expect(await screen.findByRole('cell', { name: 'Mathematics' })).toBeTruthy()
    expect(calls('/api/v1/report-cards/grades')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
    expect(screen.getByText('86.5')).toBeTruthy()
    expect(screen.getByText('Passed')).toBeTruthy()
    expect(screen.getByText('Below minimum')).toBeTruthy()
    expect(screen.getByText('Understands fractions well.')).toBeTruthy()
    expect(screen.getByText(/Published on 18\/12\/2026/)).toBeTruthy()
  })

  it('says the grades are not published yet instead of showing an error', async () => {
    http.customInstance.mockRejectedValue(apiError(404, { code: 'grades.not_published' }))
    renderPage(<MyGradesPage />)

    expect(await screen.findByText('Not published yet')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows an error with a retry for any other failure', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    renderPage(<MyGradesPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The grades could not be loaded.')
  })

  it('opens the PDF report card through its short-lived address', async () => {
    http.customInstance.mockImplementation(async ({ url }: { url: string }) =>
      url === '/api/v1/report-cards/url' ? { url: 'https://storage.test/report.pdf?sig=1', expiresAt: '2026-12-18T03:10:00Z' } : grades(),
    )
    renderPage(<MyGradesPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Download report card (PDF)' }))

    await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/report.pdf?sig=1'))
    expect(calls('/api/v1/report-cards/url')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
  })

  it('has no download button until the report cards are generated', async () => {
    http.customInstance.mockResolvedValue(grades({ hasReportCard: false }))
    renderPage(<MyGradesPage />)

    await screen.findByRole('cell', { name: 'Mathematics' })
    expect(screen.queryByRole('button', { name: 'Download report card (PDF)' })).toBeNull()
  })
})

describe('ChildGradesPage', () => {
  beforeEach(() => signInAs('Parent', 'Ibu Putri'))

  it('shows the grades of the chosen child', async () => {
    http.customInstance.mockResolvedValue(grades())
    renderPage(<ChildGradesPage />)

    expect(await screen.findByRole('cell', { name: 'Natural Sciences' })).toBeTruthy()
    expect(calls('/api/v1/report-cards/grades')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
  })
})
