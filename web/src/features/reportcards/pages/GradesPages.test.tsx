import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useChildStore } from '@/stores/useChildStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { StudentGrades, StudentTerm } from '../api/reportCards'
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

const terms: StudentTerm[] = [
  { id: 't0', name: 'Term 2', academicYearName: '2025/2026', isActive: false, isPublished: true },
  { id: 't1', name: 'Term 1', academicYearName: '2026/2027', isActive: true, isPublished: false },
]

type Handlers = { grades?: () => unknown; terms?: StudentTerm[]; url?: unknown }

/** The API as the family pages see it: terms of the student's year, the published grades, and the PDF address. */
function serve({ grades: gradesOf = () => grades(), terms: termsOf = terms, url }: Handlers = {}) {
  http.customInstance.mockImplementation(async ({ url: path }: { url: string }) => {
    if (path === '/api/v1/report-cards/terms') return termsOf
    if (path === '/api/v1/report-cards/url') return url
    if (path === '/api/v1/report-cards/grades') return gradesOf()
    throw new Error(`unexpected request ${path}`)
  })
}

const calls = (url: string) => http.customInstance.mock.calls.filter(([c]) => c.url === url).map(([c]) => c)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  open.tab.mockReset()
  useChildStore.setState({ childId: null })
  api.getMyAttendance.mockResolvedValue({ studentId: 's1' })
  api.listMyChildren.mockResolvedValue([{ studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' }])
  // Students and parents may not read the academic years: the pages must never ask for them.
  api.listAcademicYears.mockRejectedValue(apiError(403))
})

describe('MyGradesPage', () => {
  beforeEach(() => signInAs('Student', 'Rina Putri'))

  it('shows the published grades of the current term with the band, result and report texts', async () => {
    serve()
    renderPage(<MyGradesPage />)

    expect(await screen.findByRole('cell', { name: 'Mathematics' })).toBeTruthy()
    expect(calls('/api/v1/report-cards/terms')[0]?.params).toEqual({ studentId: 's1' })
    expect(calls('/api/v1/report-cards/grades')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
    expect(api.listAcademicYears).not.toHaveBeenCalled()
    expect(screen.getByText('86.5')).toBeTruthy()
    expect(screen.getByText('Passed')).toBeTruthy()
    expect(screen.getByText('Below minimum')).toBeTruthy()
    expect(screen.getByText('Understands fractions well.')).toBeTruthy()
    expect(screen.getByText(/Published on 18\/12\/2026/)).toBeTruthy()
  })

  it('says the grades are not published yet instead of showing an error', async () => {
    serve({
      grades: () => {
        throw apiError(404, { code: 'grades.not_published' })
      },
    })
    renderPage(<MyGradesPage />)

    expect(await screen.findByText('Not published yet')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows an error with a retry for any other failure', async () => {
    serve({
      grades: () => {
        throw apiError(500)
      },
    })
    renderPage(<MyGradesPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The grades could not be loaded.')
  })

  it('opens the PDF report card through its short-lived address', async () => {
    serve({ url: { url: 'https://storage.test/report.pdf?sig=1', expiresAt: '2026-12-18T03:10:00Z' } })
    renderPage(<MyGradesPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Download report card (PDF)' }))

    await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/report.pdf?sig=1'))
    expect(calls('/api/v1/report-cards/url')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
  })

  it('offers the terms of the year of the student with the current term preselected, and loads another on choice', async () => {
    serve()
    renderPage(<MyGradesPage />)

    const picker = (await screen.findByLabelText('Term')) as HTMLSelectElement
    expect(Array.from(picker.options).map((o) => o.textContent)).toEqual(['2025/2026 - Term 2', '2026/2027 - Term 1'])
    expect(picker.value).toBe('t1')

    fireEvent.change(picker, { target: { value: 't0' } })

    await waitFor(() => expect(calls('/api/v1/report-cards/grades').at(-1)?.params).toEqual({ studentId: 's1', termId: 't0' }))
  })

  it('falls back to the first term when none is current', async () => {
    serve({ terms: terms.map((term) => ({ ...term, isActive: false })) })
    renderPage(<MyGradesPage />)

    expect(((await screen.findByLabelText('Term')) as HTMLSelectElement).value).toBe('t0')
    await waitFor(() => expect(calls('/api/v1/report-cards/grades')[0]?.params).toEqual({ studentId: 's1', termId: 't0' }))
  })

  it('works while the academic years are forbidden to families', async () => {
    serve()
    renderPage(<MyGradesPage />)

    expect(await screen.findByRole('cell', { name: 'Mathematics' })).toBeTruthy()
    expect(screen.queryByText('The grades could not be loaded.')).toBeNull()
    expect(api.listAcademicYears).not.toHaveBeenCalled()
  })

  it('says there is no term when the year has none', async () => {
    serve({ terms: [] })
    renderPage(<MyGradesPage />)

    expect(await screen.findByText('There is no term yet.')).toBeTruthy()
  })

  it('has no download button until the report cards are generated', async () => {
    serve({ grades: () => grades({ hasReportCard: false }) })
    renderPage(<MyGradesPage />)

    await screen.findByRole('cell', { name: 'Mathematics' })
    expect(screen.queryByRole('button', { name: 'Download report card (PDF)' })).toBeNull()
  })
})

describe('ChildGradesPage', () => {
  beforeEach(() => signInAs('Parent', 'Ibu Putri'))

  it('shows the grades of the chosen child', async () => {
    serve()
    renderPage(<ChildGradesPage />)

    expect(await screen.findByRole('cell', { name: 'Natural Sciences' })).toBeTruthy()
    expect(calls('/api/v1/report-cards/grades')[0]?.params).toEqual({ studentId: 's1', termId: 't1' })
  })
})
