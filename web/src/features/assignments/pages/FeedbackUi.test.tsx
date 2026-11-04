import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { FeedbackDraft } from '../api/feedback'
import { ReviewSection } from '../components/ReviewSection'

const api = vi.hoisted(() => ({ listSubmissions: vi.fn(), getSubmission: vi.fn(), gradeSubmission: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const calls = (method: string, urlPart: string) =>
  http.customInstance.mock.calls.map(([c]) => c as { url: string; method: string; data?: unknown }).filter((c) => c.method === method && c.url.includes(urlPart))

const row = (overrides: Record<string, unknown> = {}) => ({
  studentId: 's1',
  studentName: 'Rina Putri',
  classroomName: 'Class A',
  submissionId: 'sub1',
  status: 'Submitted',
  submittedAt: '2026-10-28T02:00:00Z',
  score: null,
  ...overrides,
})

const work = (overrides: Record<string, unknown> = {}) => ({
  id: 'sub1',
  assignmentId: 'as1',
  studentId: 's1',
  studentName: 'Rina Putri',
  fileName: null,
  hasFile: false,
  textBody: 'Schools should start later.',
  submittedAt: '2026-10-28T02:00:00Z',
  isLate: false,
  score: null,
  feedback: null,
  gradedAt: null,
  ...overrides,
})

const draft = (overrides: Partial<FeedbackDraft> = {}): FeedbackDraft => ({
  submissionId: 'sub1',
  status: 'Draft',
  criteria: [
    { name: 'Argument', maxPoints: 10, points: 8, reason: 'Two clear reasons.' },
    { name: 'Structure', maxPoints: 5, points: 3, reason: 'No conclusion.' },
  ],
  suggestion: 'Add a conclusion.',
  score: 73.3,
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

const rubric = { assignmentId: 'as1', criteria: [{ name: 'Argument', description: 'A clear position', maxPoints: 10 }], totalPoints: 10 }

/** The API of AI-05, answering by address; a test overrides what it needs. */
function serve(options: { drafts?: unknown[]; draftOf?: FeedbackDraft | null } = {}) {
  http.customInstance.mockImplementation(({ url, method }: { url: string; method: string }) => {
    if (method === 'GET' && url.includes('/essay-feedback/assignments/')) return Promise.resolve(options.drafts ?? [])
    if (method === 'GET' && url.includes('/essay-feedback/submissions/')) {
      return options.draftOf ? Promise.resolve(options.draftOf) : Promise.reject(apiError(404, { code: 'feedback.not_found' }))
    }
    if (method === 'GET' && url.includes('/rubrics/')) return Promise.resolve(rubric)
    if (method === 'PUT' && url.includes('/rubrics/')) return Promise.resolve(rubric)
    if (method === 'POST' && url.endsWith('/draft') && url.includes('/submissions/')) return Promise.resolve(draft())
    if (method === 'POST' && url.endsWith('/draft')) return Promise.resolve({ drafted: 2, alreadyHadDraft: 0, notText: 0, alreadyScored: 0, failed: 0, stoppedBy: null })
    if (method === 'POST' && url.endsWith('/accept')) return Promise.resolve(draft({ status: 'Accepted' }))
    if (method === 'POST' && url.endsWith('/reject')) return Promise.resolve(draft({ status: 'Rejected' }))
    return Promise.reject(new Error(`unexpected ${method} ${url}`))
  })
}

const renderReview = (canScore = true) => renderPage(<ReviewSection assignmentId="as1" canScore={canScore} />, '/assignments/as1')

async function openReview() {
  fireEvent.click(await screen.findByRole('button', { name: /Review/ }))
  return screen.findByRole('dialog')
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  useToastStore.setState({ toasts: [] })
  api.listSubmissions.mockResolvedValue([row()])
  api.getSubmission.mockResolvedValue(work())
  signInAs('Teacher', 'Budi Santoso')
})

describe('AI-05 in the review of handed-in work', () => {
  it('gives the teacher of the assignment the rubric and the bulk draft, and shows the state of each draft', async () => {
    serve({ drafts: [{ submissionId: 'sub1', status: 'Draft', score: 73.3 }] })

    renderReview()

    expect(await screen.findByRole('button', { name: 'Rubric' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Draft feedback for all' })).toBeTruthy()
    const table = await screen.findByRole('table')
    await waitFor(() => expect(within(table).getByText('Draft')).toBeTruthy())
  })

  it('shows neither to someone who may not score the work', async () => {
    signInAs('Principal', 'Hendra Wirawan')
    serve()

    renderReview(false)

    await screen.findByRole('table')
    expect(screen.queryByRole('button', { name: 'Rubric' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Draft feedback for all' })).toBeNull()
    expect(http.customInstance).not.toHaveBeenCalled()
  })

  it('sets the rubric: loads the criteria, adds one and saves them all', async () => {
    serve()
    renderReview()

    fireEvent.click(await screen.findByRole('button', { name: 'Rubric' }))
    const dialog = await screen.findByRole('dialog')
    expect(((await within(dialog).findByLabelText(/^Criterion 1/)) as HTMLInputElement).value).toBe('Argument')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add a criterion' }))
    fireEvent.change(within(dialog).getByLabelText(/^Criterion 2/), { target: { value: 'Structure' } })
    fireEvent.change(within(dialog).getByLabelText(/^Highest score of criterion 2/), { target: { value: '5' } })
    expect(within(dialog).getByText('Highest total: 15 points')).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save rubric' }))

    await waitFor(() => expect(calls('PUT', '/rubrics/as1')).toHaveLength(1))
    expect(calls('PUT', '/rubrics/as1')[0]?.data).toEqual({
      criteria: [
        { name: 'Argument', description: 'A clear position', maxPoints: 10 },
        { name: 'Structure', description: '', maxPoints: 5 },
      ],
    })
  })

  it('does not let a rubric without a name be saved, and allows at most six criteria', async () => {
    serve()
    renderReview()
    fireEvent.click(await screen.findByRole('button', { name: 'Rubric' }))
    const dialog = await screen.findByRole('dialog')

    await within(dialog).findByRole('button', { name: 'Add a criterion' })
    for (let i = 0; i < 5; i++) fireEvent.click(within(dialog).getByRole('button', { name: 'Add a criterion' }))

    expect((within(dialog).getByRole('button', { name: 'Add a criterion' }) as HTMLButtonElement).disabled).toBe(true)
    expect((within(dialog).getByRole('button', { name: 'Save rubric' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('drafts the feedback of the whole assignment and says how many drafts were written', async () => {
    serve()
    renderReview()

    fireEvent.click(await screen.findByRole('button', { name: 'Draft feedback for all' }))

    await waitFor(() => expect(calls('POST', '/essay-feedback/assignments/as1/draft')).toHaveLength(1))
    await waitFor(() => expect(toasts()).toContain('success:2 drafts written.'))
  })

  it('tells the teacher when a bulk run stopped at the daily limit, and when there is no rubric', async () => {
    serve()
    http.customInstance.mockImplementationOnce(() => Promise.resolve([])) // the list of drafts
    http.customInstance.mockImplementationOnce(() => Promise.resolve({ drafted: 1, alreadyHadDraft: 0, notText: 0, alreadyScored: 0, failed: 0, stoppedBy: 'ai.daily_limit' }))
    const { unmount } = renderReview()
    fireEvent.click(await screen.findByRole('button', { name: 'Draft feedback for all' }))
    expect((await screen.findByRole('alert')).textContent).toContain('You have used your AI calls of today.')
    unmount()

    serve()
    http.customInstance.mockImplementationOnce(() => Promise.resolve([]))
    http.customInstance.mockImplementationOnce(() => Promise.reject(apiError(409, { code: 'feedback.no_rubric' })))
    renderReview()
    fireEvent.click(await screen.findByRole('button', { name: 'Draft feedback for all' }))
    expect((await screen.findByRole('alert')).textContent).toContain('Give the assignment a rubric first')
  })

  it('drafts the feedback of one piece of work from its review', async () => {
    serve()
    renderReview()
    const dialog = await openReview()

    fireEvent.click(await within(dialog).findByRole('button', { name: 'Draft feedback with AI' }))

    await waitFor(() => expect(calls('POST', '/essay-feedback/submissions/sub1/draft')).toHaveLength(1))
  })

  it('explains that a file cannot be read, and offers nothing for work that is already scored', async () => {
    serve()
    api.getSubmission.mockResolvedValue(work({ textBody: null, hasFile: true, fileName: 'essay.pdf' }))
    const { unmount } = renderReview()
    const dialog = await openReview()
    expect(await within(dialog).findByText('The AI can only read work that was typed in; this work is a file.')).toBeTruthy()
    unmount()

    api.listSubmissions.mockResolvedValue([row({ score: 70, status: 'Graded' })])
    api.getSubmission.mockResolvedValue(work({ score: 70 }))
    renderReview()
    const scored = await openReview()
    await within(scored).findByLabelText(/^Score \(0 to 100\)/)
    expect(within(scored).queryByRole('button', { name: 'Draft feedback with AI' })).toBeNull()
  })

  it('shows the draft for the teacher to edit, and the score follows the points', async () => {
    serve({ draftOf: draft() })
    renderReview()
    const dialog = await openReview()

    expect((await within(dialog).findByLabelText('Points for Argument (0 to 10)') as HTMLInputElement).value).toBe('8')
    expect((within(dialog).getByLabelText('Reason for Structure') as HTMLInputElement).value).toBe('No conclusion.')
    expect(within(dialog).getByText('Score from these points: 73.3')).toBeTruthy()
    fireEvent.change(within(dialog).getByLabelText('Points for Structure (0 to 5)'), { target: { value: '5' } })

    expect(within(dialog).getByText('Score from these points: 86.7')).toBeTruthy()
  })

  it('accepts the draft with the teachers edits', async () => {
    serve({ draftOf: draft() })
    renderReview()
    const dialog = await openReview()
    fireEvent.change(await within(dialog).findByLabelText('Points for Argument (0 to 10)'), { target: { value: '9' } })
    fireEvent.change(within(dialog).getByLabelText('Suggestion for the student'), { target: { value: 'Write a short conclusion.' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Accept and score' }))

    await waitFor(() => expect(calls('POST', '/submissions/sub1/accept')).toHaveLength(1))
    expect(calls('POST', '/submissions/sub1/accept')[0]?.data).toEqual({
      criteria: [
        { points: 9, reason: 'Two clear reasons.' },
        { points: 3, reason: 'No conclusion.' },
      ],
      suggestion: 'Write a short conclusion.',
    })
  })

  it('does not accept points above the maximum or an empty reason', async () => {
    serve({ draftOf: draft() })
    renderReview()
    const dialog = await openReview()
    const accept = await within(dialog).findByRole('button', { name: 'Accept and score' })

    fireEvent.change(within(dialog).getByLabelText('Points for Argument (0 to 10)'), { target: { value: '11' } })
    expect((accept as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(within(dialog).getByLabelText('Points for Argument (0 to 10)'), { target: { value: '10' } })
    expect((accept as HTMLButtonElement).disabled).toBe(false)
    fireEvent.change(within(dialog).getByLabelText('Reason for Argument'), { target: { value: ' ' } })
    expect((accept as HTMLButtonElement).disabled).toBe(true)
  })

  it('rejects the draft, and an accepted draft shows only its summary', async () => {
    serve({ draftOf: draft() })
    const { unmount } = renderReview()
    const dialog = await openReview()
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Reject draft' }))
    await waitFor(() => expect(calls('POST', '/submissions/sub1/reject')).toHaveLength(1))
    unmount()

    serve({ draftOf: draft({ status: 'Accepted' }) })
    renderReview()
    const done = await openReview()
    expect(await within(done).findByText('Accepted: the work was scored 73.3 from the rubric.')).toBeTruthy()
    expect(within(done).queryByRole('button', { name: 'Accept and score' })).toBeNull()
  })

  it('shows the reason when accepting fails', async () => {
    serve({ draftOf: draft() })
    http.customInstance.mockImplementation(({ url, method }: { url: string; method: string }) => {
      if (method === 'GET' && url.includes('/essay-feedback/assignments/')) return Promise.resolve([])
      if (method === 'GET') return Promise.resolve(draft())
      return Promise.reject(apiError(409, { code: 'grades.published' }))
    })
    renderReview()
    const dialog = await openReview()

    fireEvent.click(await within(dialog).findByRole('button', { name: 'Accept and score' }))

    expect(await within(dialog).findByRole('alert')).toBeTruthy()
  })
})
