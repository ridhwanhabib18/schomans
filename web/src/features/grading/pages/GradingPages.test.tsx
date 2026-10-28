import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { GradeBook } from '../api/types'
import { GradeBandsPage } from './GradeBandsPage'
import { GradeBookPage } from './GradeBookPage'

const api = vi.hoisted(() => ({ listTeachingOptions: vi.fn(), listAcademicYears: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const book = (overrides: Partial<GradeBook> = {}): GradeBook => ({
  classroomId: 'c1',
  subjectId: 'sub-math',
  termId: 't1',
  minimumPassingScore: 75,
  isPublished: false,
  weights: {
    subjectId: 'sub-math',
    isDefault: true,
    weights: [
      { component: 'Assignment', weightPercent: 30 },
      { component: 'Quiz', weightPercent: 20 },
      { component: 'Midterm', weightPercent: 20 },
      { component: 'Final', weightPercent: 30 },
    ],
  },
  rows: [
    {
      studentId: 's1',
      studentName: 'Rina Putri',
      nisn: '0012345678',
      components: [
        { component: 'Assignment', average: 88, count: 2 },
        { component: 'Quiz', average: 80, count: 1 },
        { component: 'Midterm', average: null, count: 0 },
        { component: 'Final', average: null, count: 0 },
      ],
      finalGrade: 85.2,
      band: 'B',
      isPassing: true,
      description: 'Understands fractions well.',
    },
    {
      studentId: 's2',
      studentName: 'Dimas Pratama',
      nisn: '0012345679',
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

const calls = (method: string, url: string) => http.customInstance.mock.calls.filter(([c]) => c.method === method && c.url === url).map(([c]) => c)

function serve(current: GradeBook) {
  http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
    if (url === '/api/v1/grades/book') return current
    if (url.startsWith('/api/v1/grade-weights/') && method === 'GET') return current.weights
    if (url === '/api/v1/grade-bands') return [{ id: 'b1', label: 'A', minScore: 90 }, { id: 'b2', label: 'B', minScore: 80 }, { id: 'b3', label: 'E', minScore: 0 }]
    if (url === '/api/v1/report-cards/generate') return { classroomId: 'c1', termId: 't1', count: 2, generatedAt: '2026-10-28T00:00:00Z' }
    return {}
  })
}

async function openBook() {
  renderPage(<GradeBookPage />)
  fireEvent.change(await screen.findByLabelText('Class and subject'), { target: { value: 'c1|sub-math' } })
  await screen.findByText('Rina Putri')
}

beforeEach(() => {
  http.customInstance.mockReset()
  api.listTeachingOptions.mockReset()
  api.listAcademicYears.mockReset()
  useToastStore.setState({ toasts: [] })
  signInAs('Teacher', 'Budi Santoso')
  api.listTeachingOptions.mockResolvedValue([
    { classroomId: 'c1', classroomName: 'Class A', subjectId: 'sub-math', subjectName: 'Mathematics' },
  ])
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

describe('GradeBookPage', () => {
  it('asks the teacher to choose, then shows the grades with the current term preselected', async () => {
    serve(book())
    renderPage(<GradeBookPage />)

    expect(await screen.findByText('Choose a class, a subject and a term to open the grade book.')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Class and subject'), { target: { value: 'c1|sub-math' } })

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
    expect((screen.getByLabelText('Term') as HTMLSelectElement).value).toBe('t1')
    expect(calls('GET', '/api/v1/grades/book')[0]?.params).toEqual({ classroomId: 'c1', subjectId: 'sub-math', termId: 't1' })
    expect(screen.getByText('Not published')).toBeTruthy()
    expect(screen.getByText('Minimum passing score: 75')).toBeTruthy()
    expect(screen.getByText('Passed')).toBeTruthy()
    expect(screen.getByText('Below minimum')).toBeTruthy()
  })

  it('says so when the teacher has no class', async () => {
    api.listTeachingOptions.mockResolvedValue([])
    renderPage(<GradeBookPage />)

    expect(await screen.findByText('You do not teach any class yet, so there is no grade book to open.')).toBeTruthy()
  })

  it('saves a typed score when the field loses focus', async () => {
    serve(book())
    await openBook()

    const field = screen.getByLabelText('Midterm score of Rina Putri')
    fireEvent.change(field, { target: { value: '90' } })
    fireEvent.blur(field)

    await waitFor(() => expect(calls('PUT', '/api/v1/grades')).toHaveLength(1))
    expect(calls('PUT', '/api/v1/grades')[0]?.data).toEqual({ studentId: 's1', subjectId: 'sub-math', termId: 't1', component: 'Midterm', score: 90 })
    expect(useToastStore.getState().toasts.map((x) => x.title)).toContain('Score saved')
  })

  it('does not send a score outside 0 to 100', async () => {
    serve(book())
    await openBook()

    const field = screen.getByLabelText('Final exam score of Rina Putri')
    fireEvent.change(field, { target: { value: '120' } })
    fireEvent.blur(field)

    await waitFor(() => expect(useToastStore.getState().toasts.map((x) => x.title)).toContain('A score is between 0 and 100.'))
    expect(calls('PUT', '/api/v1/grades')).toHaveLength(0)
  })

  it('shows the API reason when a score is refused because the grades are published', async () => {
    serve(book())
    await openBook()
    http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
      if (method === 'PUT' && url === '/api/v1/grades') throw apiError(409, { code: 'grades.published' })
      return book()
    })

    const field = screen.getByLabelText('Quiz score of Rina Putri')
    fireEvent.change(field, { target: { value: '70' } })
    fireEvent.blur(field)

    await waitFor(() =>
      expect(useToastStore.getState().toasts.map((x) => x.title)).toContain('The grades of this term are published. Unpublish them before changing a score or a text.'),
    )
  })

  it('locks the scores of published grades and offers to unpublish', async () => {
    serve(book({ isPublished: true }))
    await openBook()

    expect(screen.getByText('Published')).toBeTruthy()
    expect((screen.getByLabelText('Quiz score of Rina Putri') as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByRole('button', { name: 'Unpublish grades' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Publish grades' })).toBeNull()
    expect((screen.getByRole('button', { name: 'Generate report cards' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('publishes the grades after a confirmation, and cannot generate report cards before that', async () => {
    serve(book())
    await openBook()

    expect((screen.getByRole('button', { name: 'Generate report cards' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Publish grades' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(calls('POST', '/api/v1/grades/publications')).toHaveLength(0)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish the grades of this class?' }))

    await waitFor(() => expect(calls('POST', '/api/v1/grades/publications')).toHaveLength(1))
    expect(calls('POST', '/api/v1/grades/publications')[0]?.data).toEqual({ classroomId: 'c1', termId: 't1' })
  })

  it('shows the reason in the dialog when only the homeroom teacher may publish', async () => {
    serve(book())
    await openBook()
    http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
      if (method === 'POST' && url === '/api/v1/grades/publications') throw apiError(403, { code: 'grades.not_homeroom' })
      return book()
    })

    fireEvent.click(screen.getByRole('button', { name: 'Publish grades' }))
    const dialog = await screen.findByRole('alertdialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish the grades of this class?' }))

    expect(await within(dialog).findByText('Only the homeroom teacher of the class can do this.')).toBeTruthy()
  })

  it('generates the report cards of a published class and reports how many', async () => {
    serve(book({ isPublished: true }))
    await openBook()

    fireEvent.click(screen.getByRole('button', { name: 'Generate report cards' }))
    const dialog = await screen.findByRole('alertdialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Generate the report cards?' }))

    await waitFor(() => expect(calls('POST', '/api/v1/report-cards/generate')).toHaveLength(1))
    await waitFor(() => expect(useToastStore.getState().toasts.map((x) => x.title)).toContain('2 report cards generated'))
  })

  it('writes a report text for a student', async () => {
    serve(book())
    await openBook()

    fireEvent.click(screen.getByRole('button', { name: /Report text.*Dimas Pratama/ }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText('What the student achieved in this subject'), { target: { value: 'Needs practice with fractions.' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save text' }))

    await waitFor(() => expect(calls('PUT', '/api/v1/report-cards/descriptions')).toHaveLength(1))
    expect(calls('PUT', '/api/v1/report-cards/descriptions')[0]?.data).toEqual({
      studentId: 's2',
      subjectId: 'sub-math',
      termId: 't1',
      body: 'Needs practice with fractions.',
    })
  })

  it('only saves weights that add up to 100', async () => {
    serve(book())
    await openBook()

    fireEvent.click(screen.getByRole('button', { name: 'Weights' }))
    const dialog = await screen.findByRole('dialog')
    const save = await within(dialog).findByRole('button', { name: 'Save weights' })
    expect((save as HTMLButtonElement).disabled).toBe(false)

    fireEvent.change(within(dialog).getByLabelText('Quiz'), { target: { value: '30' } })
    expect(within(dialog).getByText(/Total: 110/)).toBeTruthy()
    expect((save as HTMLButtonElement).disabled).toBe(true)

    fireEvent.change(within(dialog).getByLabelText('Assignments'), { target: { value: '20' } })
    expect((save as HTMLButtonElement).disabled).toBe(false)
    fireEvent.click(save)

    await waitFor(() => expect(calls('PUT', '/api/v1/grade-weights/sub-math')).toHaveLength(1))
    expect(calls('PUT', '/api/v1/grade-weights/sub-math')[0]?.data.weights).toEqual([
      { component: 'Assignment', weightPercent: 20 },
      { component: 'Quiz', weightPercent: 30 },
      { component: 'Midterm', weightPercent: 20 },
      { component: 'Final', weightPercent: 30 },
    ])
  })
})

describe('GradeBandsPage', () => {
  beforeEach(() => signInAs('Admin', 'Sari Dewi'))

  it('lists the bands, highest first, and saves a changed scale', async () => {
    serve(book())
    renderPage(<GradeBandsPage />)

    const labels = (await screen.findAllByLabelText('Label')) as HTMLInputElement[]
    expect(labels.map((input) => input.value)).toEqual(['A', 'B', 'E'])

    fireEvent.change(screen.getAllByLabelText('From score')[1]!, { target: { value: '85' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save bands' }))

    await waitFor(() => expect(calls('PUT', '/api/v1/grade-bands')).toHaveLength(1))
    expect(calls('PUT', '/api/v1/grade-bands')[0]?.data).toEqual({
      bands: [
        { label: 'A', minScore: 90 },
        { label: 'B', minScore: 85 },
        { label: 'E', minScore: 0 },
      ],
    })
  })

  it('refuses a scale without a band at 0, with the same label twice, or with an empty band', async () => {
    serve(book())
    renderPage(<GradeBandsPage />)
    await screen.findAllByLabelText('Label')
    const save = screen.getByRole('button', { name: 'Save bands' }) as HTMLButtonElement

    fireEvent.change(screen.getAllByLabelText('From score')[2]!, { target: { value: '10' } })
    expect(screen.getByText('One band must start at 0, so every grade gets a label.')).toBeTruthy()
    expect(save.disabled).toBe(true)

    fireEvent.change(screen.getAllByLabelText('From score')[2]!, { target: { value: '0' } })
    fireEvent.change(screen.getAllByLabelText('Label')[1]!, { target: { value: 'a' } })
    expect(screen.getByText('Every band needs its own label and its own starting score.')).toBeTruthy()
    expect(save.disabled).toBe(true)

    fireEvent.change(screen.getAllByLabelText('Label')[1]!, { target: { value: 'B' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add band' }))
    expect(screen.getByText('Fill in a label and a score from 0 to 100 for every band.')).toBeTruthy()
    expect(save.disabled).toBe(true)
    expect(calls('PUT', '/api/v1/grade-bands')).toHaveLength(0)
  })
})
