import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { currentMonth, shiftMonth } from '@/shared/lib/time'
import { useChildStore } from '@/stores/useChildStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { MyClassroom, ProgressSummary, TeacherNote } from '../api/types'
import { ChildSummariesPage } from './MySummariesPage'
import { ReviewSummariesPage } from './ReviewSummariesPage'
import { TeacherNotesPage } from './TeacherNotesPage'

const api = vi.hoisted(() => ({ listStudents: vi.fn(), listMyChildren: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const LAST_MONTH = shiftMonth(currentMonth(), -1)

const BODY = 'S is doing well.\n\nWhat is going well: Steady effort.\nWhat needs attention: Nothing stands out.\nA suggestion for home: Read together.'

const summary = (overrides: Partial<ProgressSummary> = {}): ProgressSummary => ({
  id: 'p1',
  studentId: 's1',
  studentName: 'Rina Putri',
  month: LAST_MONTH,
  body: BODY,
  status: 'Draft',
  isAiDrafted: true,
  sentAt: null,
  ...overrides,
})

const homeroom: MyClassroom = { id: 'c1', name: 'Class A', isHomeroom: true }

const calls = (method: string, url: string) => http.customInstance.mock.calls.filter(([c]) => c.method === method && c.url === url).map(([c]) => c)

interface Server {
  classrooms?: MyClassroom[]
  summaries?: ProgressSummary[]
  notes?: TeacherNote[]
  mine?: ProgressSummary[]
}

function serve({ classrooms = [homeroom], summaries = [], notes = [], mine = [] }: Server = {}) {
  http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
    if (url === '/api/v1/teacher-notes/classrooms') return classrooms
    if (url === '/api/v1/progress-summaries' && method === 'GET') return summaries
    if (url === '/api/v1/progress-summaries/mine') return pageOf(mine)
    if (url === '/api/v1/teacher-notes' && method === 'GET') return pageOf(notes)
    if (url === '/api/v1/progress-summaries/send') return { sent: 1, parentsNotified: 2 }
    if (url.endsWith('/approve')) return summary({ status: 'Approved' })
    if (url === '/api/v1/progress-summaries/regenerate') return summary()
    if (method === 'PUT') return summary({ isAiDrafted: false })
    if (url === '/api/v1/teacher-notes' && method === 'POST') return {}
    return {}
  })
}

beforeEach(() => {
  http.customInstance.mockReset()
  api.listStudents.mockReset()
  api.listMyChildren.mockReset()
  useToastStore.setState({ toasts: [] })
  useChildStore.setState({ childId: null })
  signInAs('Teacher', 'Budi Santoso')
  api.listStudents.mockResolvedValue(pageOf([{ id: 's1', fullName: 'Rina Putri' }, { id: 's2', fullName: 'Dimas Pratama' }]))
  api.listMyChildren.mockResolvedValue([{ studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' }])
})

describe('ReviewSummariesPage', () => {
  it('lists the drafts of the previous month with their status and who wrote the text', async () => {
    serve({ summaries: [summary(), summary({ id: 'p2', studentId: 's2', studentName: 'Dimas Pratama', status: 'Approved', isAiDrafted: false })] })
    renderPage(<ReviewSummariesPage />)

    expect(await screen.findByText('Rina Putri', { selector: 'h3' })).toBeTruthy()
    expect(calls('GET', '/api/v1/progress-summaries')[0]?.params).toEqual({ classroomId: 'c1', month: LAST_MONTH })
    expect(screen.getByText('Draft')).toBeTruthy()
    expect(screen.getByText('AI draft')).toBeTruthy()
    expect(screen.getByText('Approved')).toBeTruthy()
    expect(screen.getByText('Edited by you')).toBeTruthy()
    expect(screen.getAllByText('Steady effort.').length).toBe(2)
    expect(screen.getByText('2 summaries: 1 approved, 0 sent')).toBeTruthy()
  })

  it('says so when the teacher is not the homeroom teacher of a classroom', async () => {
    serve({ classrooms: [{ id: 'c9', name: 'Class Z', isHomeroom: false }] })
    renderPage(<ReviewSummariesPage />)

    expect(await screen.findByText(/not the homeroom teacher of a classroom this year/)).toBeTruthy()
    expect(calls('GET', '/api/v1/progress-summaries')).toHaveLength(0)
  })

  it('approves a draft and tells the teacher', async () => {
    serve({ summaries: [summary()] })
    renderPage(<ReviewSummariesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Approve: Rina Putri/ }))

    await waitFor(() => expect(calls('POST', '/api/v1/progress-summaries/p1/approve')).toHaveLength(1))
    expect(useToastStore.getState().toasts[0]?.title).toBe('Summary approved.')
  })

  it('sends only after the teacher confirms, and only when something is approved', async () => {
    serve({ summaries: [summary({ status: 'Approved' }), summary({ id: 'p2', studentId: 's2', studentName: 'Dimas Pratama' })] })
    renderPage(<ReviewSummariesPage />)

    const send = await screen.findByRole('button', { name: 'Send approved (1)' })
    fireEvent.click(send)
    expect(calls('POST', '/api/v1/progress-summaries/send')).toHaveLength(0)
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Send to parents' }))

    await waitFor(() => expect(calls('POST', '/api/v1/progress-summaries/send')).toHaveLength(1))
    expect(calls('POST', '/api/v1/progress-summaries/send')[0]?.data).toEqual({ classroomId: 'c1', month: LAST_MONTH })
    expect(useToastStore.getState().toasts[0]?.title).toBe('Sent 1 summary(ies); 2 parent(s) notified.')
  })

  it('keeps the send button off while nothing is approved', async () => {
    serve({ summaries: [summary()] })
    renderPage(<ReviewSummariesPage />)

    const send = await screen.findByRole('button', { name: 'Send approved (0)' })

    expect((send as HTMLButtonElement).disabled).toBe(true)
  })

  it('opens the editor and saves the new text', async () => {
    serve({ summaries: [summary()] })
    renderPage(<ReviewSummariesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Edit: Rina Putri/ }))
    fireEvent.change(await screen.findByLabelText('Text for the parents'), { target: { value: 'My own words.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save text' }))

    await waitFor(() => expect(calls('PUT', '/api/v1/progress-summaries/p1')).toHaveLength(1))
    expect(calls('PUT', '/api/v1/progress-summaries/p1')[0]?.data).toEqual({ id: 'p1', body: 'My own words.' })
  })

  it('locks a sent summary', async () => {
    serve({ summaries: [summary({ status: 'Sent' })] })
    renderPage(<ReviewSummariesPage />)

    expect(((await screen.findByRole('button', { name: /Edit: Rina Putri/ })) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: /New draft: Rina Putri/ }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: /Approve: Rina Putri/ }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('shows the message of the daily AI limit when a new draft is refused', async () => {
    serve({ summaries: [summary()] })
    http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
      if (url === '/api/v1/teacher-notes/classrooms') return [homeroom]
      if (url === '/api/v1/progress-summaries' && method === 'GET') return [summary()]
      throw apiError(429, { code: 'ai.daily_limit', title: 'raw title' })
    })
    renderPage(<ReviewSummariesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /New draft: Rina Putri/ }))

    expect(await screen.findByText('You have used your AI calls of today. Try again tomorrow.')).toBeTruthy()
  })

  it('offers a draft for students who have no summary yet', async () => {
    serve({ summaries: [summary()] })
    renderPage(<ReviewSummariesPage />)

    expect(await screen.findByText('Students without a summary')).toBeTruthy()
    fireEvent.click(await screen.findByRole('button', { name: /Write a draft: Dimas Pratama/ }))

    await waitFor(() => expect(calls('POST', '/api/v1/progress-summaries/regenerate')[0]?.data).toEqual({ studentId: 's2', month: LAST_MONTH }))
  })

  it('shows an empty state when the month has no summaries', async () => {
    serve()
    renderPage(<ReviewSummariesPage />)

    expect(await screen.findByText(/There are no summaries for this month yet/)).toBeTruthy()
  })
})

describe('TeacherNotesPage', () => {
  it('lists the students of the classroom and opens their notes', async () => {
    serve({ notes: [{ id: 'n1', studentId: 's1', authorName: 'Budi Santoso', date: '2026-10-12', body: 'Helped a classmate.', isMine: true }] })
    renderPage(<TeacherNotesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Notes: Rina Putri/ }))

    expect(await screen.findByText('Helped a classmate.')).toBeTruthy()
    expect(screen.getByText('Budi Santoso on 12/10/2026')).toBeTruthy()
    expect(calls('GET', '/api/v1/teacher-notes').pop()?.params).toMatchObject({ studentId: 's1' })
  })

  it('adds a note', async () => {
    serve()
    renderPage(<TeacherNotesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Notes: Rina Putri/ }))
    fireEvent.change(await screen.findByLabelText('New note'), { target: { value: 'Calm and focused.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    await waitFor(() => expect(calls('POST', '/api/v1/teacher-notes')[0]?.data).toMatchObject({ studentId: 's1', body: 'Calm and focused.' }))
  })

  it('offers delete only on the notes the teacher wrote', async () => {
    serve({
      notes: [
        { id: 'n1', studentId: 's1', authorName: 'Budi Santoso', date: '2026-10-12', body: 'Mine.', isMine: true },
        { id: 'n2', studentId: 's1', authorName: 'Sri Wahyuni', date: '2026-10-10', body: 'Hers.', isMine: false },
      ],
    })
    renderPage(<TeacherNotesPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Notes: Rina Putri/ }))
    await screen.findByText('Hers.')

    expect(screen.getAllByRole('button', { name: 'Delete' })).toHaveLength(1)
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(calls('DELETE', '/api/v1/teacher-notes/n1')).toHaveLength(1))
  })

  it('says so when the teacher works in no classroom', async () => {
    serve({ classrooms: [] })
    renderPage(<TeacherNotesPage />)

    expect(await screen.findByText('You do not work in any classroom this year.')).toBeTruthy()
  })
})

describe('ChildSummariesPage', () => {
  beforeEach(() => signInAs('Parent', 'Ibu Putri'))

  it('shows the sent summaries of the child, newest first, with the three points', async () => {
    serve({ mine: [summary({ status: 'Sent', sentAt: '2026-11-02T01:00:00Z' })] })
    renderPage(<ChildSummariesPage />)

    expect(await screen.findByText('What is going well')).toBeTruthy()
    expect(screen.getByText('Steady effort.')).toBeTruthy()
    expect(screen.getByText('What needs attention')).toBeTruthy()
    expect(screen.getByText('A suggestion for home')).toBeTruthy()
    expect(screen.getByText('Sent on 02/11/2026')).toBeTruthy()
    expect(calls('GET', '/api/v1/progress-summaries/mine')[0]?.params).toMatchObject({ studentId: 's1' })
  })

  it('says so when nothing has been sent', async () => {
    serve()
    renderPage(<ChildSummariesPage />)

    expect(await screen.findByText('No summary has been sent yet.')).toBeTruthy()
  })

  it('opens the child a notification links to', async () => {
    api.listMyChildren.mockResolvedValue([
      { studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' },
      { studentId: 's2', fullName: 'Dimas Putra', classroomName: 'Class B' },
    ])
    serve()
    renderPage(<ChildSummariesPage />, '/my-children/summaries?studentId=s2')

    await waitFor(() => expect(calls('GET', '/api/v1/progress-summaries/mine').pop()?.params).toMatchObject({ studentId: 's2' }))
  })
})
