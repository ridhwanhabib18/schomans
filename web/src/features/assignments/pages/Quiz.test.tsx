import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { Quiz, QuizQuestion } from '../api/quizzes'
import { QuizAttemptDialog } from '../components/QuizAttemptDialog'
import { QuizPanel } from '../components/QuizPanel'
import { ReviewSection } from '../components/ReviewSection'
import { QuizBuilderPage } from './QuizBuilderPage'

const api = vi.hoisted(() => ({ listTeachingOptions: vi.fn(), listSubmissions: vi.fn(), getSubmission: vi.fn(), gradeSubmission: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

type Call = { url: string; method: string; data?: unknown; params?: Record<string, unknown> }
const calls = (method: string, urlPart: string) => http.customInstance.mock.calls.map(([c]) => c as Call).filter((c) => c.method === method && c.url.includes(urlPart))
const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

const mc = (overrides: Partial<QuizQuestion> = {}): QuizQuestion => ({
  questionId: 'q1',
  order: 1,
  type: 'MultipleChoice',
  difficulty: 'Easy',
  body: 'What is 2 + 2?',
  options: ['3', '4', '5'],
  points: 1,
  answerKey: null,
  explanation: null,
  myAnswer: null,
  pointsAwarded: null,
  ...overrides,
})

const sa = (overrides: Partial<QuizQuestion> = {}): QuizQuestion => ({ ...mc(), questionId: 'q2', order: 2, type: 'ShortAnswer', body: 'Explain why.', options: [], points: 2, ...overrides })

const quiz = (overrides: Partial<Quiz> = {}): Quiz => ({
  assignmentId: 'a1',
  title: 'Cells quiz',
  description: '',
  dueAt: '2026-12-01T03:00:00Z',
  isOpen: true,
  resultsVisible: false,
  canEdit: false,
  totalPoints: 3,
  questions: [mc(), sa()],
  attempt: null,
  ...overrides,
})

const attempt = (overrides: Partial<NonNullable<Quiz['attempt']>> = {}): NonNullable<Quiz['attempt']> => ({
  submissionId: 'sub1',
  studentId: 's1',
  studentName: 'Rina Putri',
  submittedAt: '2026-11-04T02:00:00Z',
  status: 'Submitted',
  score: null,
  shortAnswersToGrade: 0,
  ...overrides,
})

function serve(value: Quiz) {
  http.customInstance.mockImplementation(({ url, method }: Call) => {
    if (method === 'GET' && url.startsWith('/api/v1/quizzes/')) return Promise.resolve(value)
    if (method === 'POST' && url.endsWith('/submit')) return Promise.resolve({ ...value, attempt: attempt() })
    if (method === 'POST' && url.endsWith('/grade')) return Promise.resolve(value)
    return Promise.reject(new Error(`unexpected ${method} ${url}`))
  })
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  useToastStore.setState({ toasts: [] })
  signInAs('Student', 'Rina Putri')
})

describe('QuizPanel for a student', () => {
  it('shows the questions without a key, and says the quiz is taken once', async () => {
    serve(quiz())
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    expect(await screen.findByText(/You can take this quiz only once/)).toBeTruthy()
    expect(screen.getByText(/What is 2 \+ 2\?/)).toBeTruthy()
    expect(screen.getByLabelText('4')).toBeTruthy()
    expect(screen.queryByText('Right answer')).toBeNull()
  })

  it('hands in the answers after a confirmation, with unanswered questions as null', async () => {
    serve(quiz())
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    fireEvent.click(await screen.findByLabelText('4'))
    fireEvent.click(screen.getByRole('button', { name: 'Hand in the quiz' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(within(confirm).getByText(/1 question\(s\) have no answer yet/)).toBeTruthy()
    expect(calls('POST', '/submit')).toHaveLength(0)
    fireEvent.click(within(confirm).getByRole('button', { name: 'Hand in the quiz' }))

    await waitFor(() => expect(toasts()).toContain('success:Your quiz is handed in.'))
    expect(calls('POST', '/submit')[0]!.data).toEqual({
      assignmentId: 'a1',
      answers: [
        { questionId: 'q1', answer: '1' },
        { questionId: 'q2', answer: null },
      ],
    })
  })

  it('sends the text of a short answer', async () => {
    serve(quiz())
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    fireEvent.click(await screen.findByLabelText('3'))
    fireEvent.change(screen.getByLabelText('Your answer to question 2'), { target: { value: '  Because of cells.  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Hand in the quiz' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Hand in the quiz' }))

    await waitFor(() => expect(calls('POST', '/submit')).toHaveLength(1))
    expect((calls('POST', '/submit')[0]!.data as { answers: unknown[] }).answers).toEqual([
      { questionId: 'q1', answer: '0' },
      { questionId: 'q2', answer: 'Because of cells.' },
    ])
  })

  it('explains why the quiz could not be handed in', async () => {
    serve(quiz())
    http.customInstance.mockImplementation(({ method }: Call) =>
      method === 'POST' ? Promise.reject(apiError(409, { code: 'quizzes.closed' })) : Promise.resolve(quiz()),
    )
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    fireEvent.click(await screen.findByRole('button', { name: 'Hand in the quiz' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Hand in the quiz' }))

    expect(await screen.findByText('The quiz is closed: its due date has passed.')).toBeTruthy()
  })

  it('shows nothing of the result before the due date once the quiz is handed in', async () => {
    serve(quiz({ attempt: attempt({ status: 'Submitted' }), questions: [mc({ myAnswer: '1' })] }))
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    expect(await screen.findByText(/The result is shown after the due date/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Hand in the quiz' })).toBeNull()
    expect(screen.queryByText('Right answer')).toBeNull()
    expect(screen.queryByText(/Score/)).toBeNull()
  })

  it('shows the score, the right answers and the explanations after the due date', async () => {
    serve(
      quiz({
        isOpen: false,
        resultsVisible: true,
        attempt: attempt({ status: 'Scored', score: 66.67 }),
        questions: [
          mc({ myAnswer: '0', answerKey: '1', explanation: 'Two and two make four.', pointsAwarded: 0 }),
          sa({ myAnswer: 'cells', answerKey: 'Because of cells.', pointsAwarded: 2 }),
        ],
      }),
    )
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    expect(await screen.findByText('Score 66.67')).toBeTruthy()
    expect(screen.getByText('Right answer')).toBeTruthy()
    expect(screen.getByText('Your answer', { selector: 'span' })).toBeTruthy()
    expect(screen.getByText('Two and two make four.')).toBeTruthy()
    expect(screen.getByText('0 of 1 point(s)')).toBeTruthy()
    expect(screen.getByText('2 of 2 point(s)')).toBeTruthy()
    expect(screen.getByText('Because of cells.')).toBeTruthy()
  })

  it('says the short answers wait for the teacher', async () => {
    serve(quiz({ isOpen: false, resultsVisible: true, attempt: attempt({ status: 'WaitingForTeacher', shortAnswersToGrade: 1 }), questions: [mc({ answerKey: '1' })] }))
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    expect(await screen.findByText('1 short answer(s) wait for points from the teacher.')).toBeTruthy()
  })

  it('says the quiz is closed when it was not taken', async () => {
    serve(quiz({ isOpen: false, resultsVisible: true, questions: [mc({ answerKey: '1' })] }))
    renderPage(<QuizPanel assignmentId="a1" canTake />, '/assignments/a1')

    expect(await screen.findByText('The quiz is closed and was not taken.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Hand in the quiz' })).toBeNull()
  })
})

describe('QuizPanel for a parent', () => {
  it('asks for the quiz of the child and offers no way to take it', async () => {
    signInAs('Parent', 'Agus Putra')
    serve(quiz())
    renderPage(<QuizPanel assignmentId="a1" studentId="s1" canTake={false} />, '/assignments/a1')

    expect(await screen.findByText('Your child has not taken the quiz yet.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Hand in the quiz' })).toBeNull()
    expect(calls('GET', '/api/v1/quizzes/a1')[0]!.params).toMatchObject({ studentId: 's1' })
  })

  it('shows the result of the child after the due date', async () => {
    signInAs('Parent', 'Agus Putra')
    serve(quiz({ isOpen: false, resultsVisible: true, attempt: attempt({ status: 'Scored', score: 100 }), questions: [mc({ myAnswer: '1', answerKey: '1', pointsAwarded: 1 })] }))
    renderPage(<QuizPanel assignmentId="a1" studentId="s1" canTake={false} />, '/assignments/a1')

    expect(await screen.findByText('Score 100')).toBeTruthy()
  })
})

describe('QuizAttemptDialog', () => {
  const graded = (overrides: Partial<Quiz> = {}) =>
    quiz({
      canEdit: true,
      isOpen: false,
      resultsVisible: true,
      attempt: attempt({ status: 'WaitingForTeacher', shortAnswersToGrade: 1 }),
      questions: [mc({ myAnswer: '1', answerKey: '1', explanation: 'Because.', pointsAwarded: 1 }), sa({ myAnswer: 'My answer', answerKey: 'Model answer' })],
      ...overrides,
    })

  const open = (canScore = true) =>
    renderPage(<QuizAttemptDialog assignmentId="a1" studentId="s1" studentName="Rina Putri" canScore={canScore} onClose={vi.fn()} />, '/assignments/a1')

  it('shows the answers next to the key and waits for the points of the short answer', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(graded())
    open()

    expect(await screen.findByText('My answer')).toBeTruthy()
    expect(screen.getByText('Model answer')).toBeTruthy()
    expect(screen.getByText('1 short answer(s) wait for points from the teacher.')).toBeTruthy()
    expect(calls('GET', '/api/v1/quizzes/a1')[0]!.params).toMatchObject({ studentId: 's1' })
  })

  it('keeps the save button off until every short answer has points within its worth', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(graded())
    open()
    const save = (await screen.findByRole('button', { name: 'Save the points' })) as HTMLButtonElement
    const points = screen.getByLabelText('Points for question 2 (0 to 2)')

    expect(save.disabled).toBe(true)
    fireEvent.change(points, { target: { value: '3' } })
    expect(save.disabled).toBe(true)
    fireEvent.change(points, { target: { value: '-1' } })
    expect(save.disabled).toBe(true)
    fireEvent.change(points, { target: { value: '1.5' } })
    expect(save.disabled).toBe(false)
  })

  it('sends the points of the short answers', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(graded())
    open()

    fireEvent.change(await screen.findByLabelText('Points for question 2 (0 to 2)'), { target: { value: '1.5' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save the points' }))

    await waitFor(() => expect(toasts()).toContain('success:Points saved.'))
    expect(calls('POST', '/grade')[0]!.data).toEqual({ assignmentId: 'a1', studentId: 's1', points: [{ questionId: 'q2', points: 1.5 }] })
  })

  it('shows the score of a scored attempt and the points given, which can be corrected', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(graded({ attempt: attempt({ status: 'Scored', score: 83.33 }), questions: [mc({ myAnswer: '1', answerKey: '1', pointsAwarded: 1 }), sa({ myAnswer: 'x', answerKey: 'y', pointsAwarded: 1.5 })] }))
    open()

    expect(await screen.findByText('Score 83.33')).toBeTruthy()
    expect((screen.getByLabelText('Points for question 2 (0 to 2)') as HTMLInputElement).value).toBe('1.5')
  })

  it('is read only for the principal', async () => {
    signInAs('Principal', 'Hendra Wirawan')
    serve(graded())
    open(false)

    expect(await screen.findByText('My answer')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save the points' })).toBeNull()
    expect((screen.getByLabelText('Points for question 2 (0 to 2)') as HTMLInputElement).disabled).toBe(true)
  })

  it('says when the student has not taken the quiz', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(graded({ attempt: null }))
    open()

    expect(await screen.findByText('This student has not taken the quiz.')).toBeTruthy()
  })
})

describe('ReviewSection of a quiz', () => {
  const row = { studentId: 's1', studentName: 'Rina Putri', nisn: '1', classroomName: 'X-A', submissionId: 'sub1', submittedAt: '2026-11-04T02:00:00Z', status: 'Submitted', score: null }

  it('has no rubric and no AI feedback, and opens the quiz attempt of a student', async () => {
    signInAs('Teacher', 'Budi Santoso')
    api.listSubmissions.mockResolvedValue([row])
    serve(quiz({ canEdit: true, resultsVisible: true, isOpen: false, attempt: attempt({ status: 'WaitingForTeacher', shortAnswersToGrade: 1 }), questions: [sa({ myAnswer: 'a', answerKey: 'b' })] }))
    renderPage(<ReviewSection assignmentId="a1" canScore isQuiz />, '/assignments/a1')

    fireEvent.click(await screen.findByRole('button', { name: /Review/ }))

    expect(await screen.findByText('1 short answer(s) wait for points from the teacher.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Rubric' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Draft feedback for all' })).toBeNull()
    expect(calls('GET', '/essay-feedback')).toHaveLength(0)
  })
})

describe('QuizBuilderPage', () => {
  const bankItem = (id: string, body: string) => ({ id, subjectId: 'math', type: 'MultipleChoice', difficulty: 'Easy', body, options: ['a', 'b'], answerKey: '0', explanation: '', createdByName: 'Budi', isMine: true })

  function serveBuilder(items = [bankItem('b1', 'What is 2 + 2?'), bankItem('b2', 'What is 3 + 3?')]) {
    api.listTeachingOptions.mockResolvedValue([
      { classroomId: 'c1', classroomName: 'X-A', subjectId: 'math', subjectName: 'Mathematics' },
      { classroomId: 'c2', classroomName: 'X-B', subjectId: 'math', subjectName: 'Mathematics' },
      { classroomId: 'c1', classroomName: 'X-A', subjectId: 'bio', subjectName: 'Biology' },
    ])
    http.customInstance.mockImplementation(({ url, method }: Call) => {
      if (method === 'GET' && url === '/api/v1/question-bank') return Promise.resolve(pageOf(items, { pageSize: 100 }))
      if (method === 'POST' && url === '/api/v1/quizzes') return Promise.resolve({ id: 'new-quiz' })
      return Promise.reject(new Error(`unexpected ${method} ${url}`))
    })
  }

  const future = () => {
    const date = new Date(Date.now() + 3 * 24 * 3600 * 1000)
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}T10:00`
  }

  const render = () => renderPage(<QuizBuilderPage />, '/assignments/quizzes/new')

  beforeEach(() => signInAs('Teacher', 'Budi Santoso'))

  it('lists one subject per entry and its bank, and keeps the create button off until the quiz is complete', async () => {
    serveBuilder()
    render()
    const create = (await screen.findByRole('button', { name: 'Create the quiz' })) as HTMLButtonElement

    expect(await screen.findByText('What is 2 + 2?')).toBeTruthy()
    expect(screen.getAllByRole('option', { name: /Mathematics|Biology/ })).toHaveLength(2)
    expect(create.disabled).toBe(true)

    fireEvent.change(screen.getByLabelText(/^Title of the quiz/), { target: { value: 'Sums' } })
    fireEvent.change(screen.getByLabelText(/^Closes at/), { target: { value: future() } })
    fireEvent.click(screen.getByLabelText('X-A'))
    expect(create.disabled).toBe(true)
    fireEvent.click(screen.getByLabelText(/What is 2 \+ 2\?/))
    expect(create.disabled).toBe(false)
  })

  it('refuses a due date in the past', async () => {
    serveBuilder()
    render()

    fireEvent.change(await screen.findByLabelText(/^Closes at/), { target: { value: '2020-01-01T10:00' } })

    expect(screen.getByText('Choose a moment in the future.')).toBeTruthy()
  })

  it('creates the quiz with the questions in the order they were ticked and their points', async () => {
    serveBuilder()
    render()

    fireEvent.change(await screen.findByLabelText(/^Title of the quiz/), { target: { value: 'Sums' } })
    fireEvent.change(screen.getByLabelText(/^Closes at/), { target: { value: future() } })
    fireEvent.click(screen.getByLabelText('X-A'))
    fireEvent.click(screen.getByLabelText('X-B'))
    fireEvent.click(await screen.findByLabelText(/What is 3 \+ 3\?/))
    fireEvent.click(screen.getByLabelText(/What is 2 \+ 2\?/))
    fireEvent.change(screen.getByLabelText('Points of question 1'), { target: { value: '5' } })
    expect(screen.getByText('In the quiz: 2 question(s), 6 point(s)')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Create the quiz' }))

    await waitFor(() => expect(toasts()).toContain('success:Quiz created. The students are told.'))
    const sent = calls('POST', '/api/v1/quizzes')[0]!.data as { title: string; subjectId: string; classroomIds: string[]; questions: unknown[]; dueAt: string }
    expect(sent.title).toBe('Sums')
    expect(sent.subjectId).toBe('math')
    expect(sent.classroomIds).toEqual(['c1', 'c2'])
    expect(sent.questions).toEqual([{ questionId: 'b2', points: 5 }, { questionId: 'b1', points: 1 }])
    expect(new Date(sent.dueAt).getTime()).toBeGreaterThan(Date.now())
  })

  it('starts again when the subject changes', async () => {
    serveBuilder()
    render()
    fireEvent.click(await screen.findByLabelText('X-A'))
    fireEvent.click(await screen.findByLabelText(/What is 2 \+ 2\?/))

    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'bio' } })

    await waitFor(() => expect(calls('GET', '/api/v1/question-bank').at(-1)!.params).toMatchObject({ subjectId: 'bio' }))
    expect(screen.getByText('Tick questions of the bank to put them in the quiz, in the order you tick them.')).toBeTruthy()
    expect((screen.getByLabelText('X-A') as HTMLInputElement).checked).toBe(false)
  })

  it('points to the bank when it is empty', async () => {
    serveBuilder([])
    render()

    expect(await screen.findByText(/has no questions yet/)).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Open the question bank' })).toBeTruthy()
  })

  it('shows why the quiz could not be created', async () => {
    serveBuilder()
    http.customInstance.mockImplementation(({ method }: Call) =>
      method === 'POST' ? Promise.reject(apiError(400, { code: 'quizzes.question_other_subject' })) : Promise.resolve(pageOf([bankItem('b1', 'What is 2 + 2?')], { pageSize: 100 })),
    )
    render()

    fireEvent.change(await screen.findByLabelText(/^Title of the quiz/), { target: { value: 'Sums' } })
    fireEvent.change(screen.getByLabelText(/^Closes at/), { target: { value: future() } })
    fireEvent.click(screen.getByLabelText('X-A'))
    fireEvent.click(await screen.findByLabelText(/What is 2 \+ 2\?/))
    fireEvent.click(screen.getByRole('button', { name: 'Create the quiz' }))

    expect(await screen.findByText('Every question must be from the question bank of the subject of the quiz.')).toBeTruthy()
  })

  it('says so when the teacher teaches nothing', async () => {
    api.listTeachingOptions.mockResolvedValue([])
    http.customInstance.mockResolvedValue(pageOf([]))
    render()

    expect(await screen.findByText(/timetable|classroom/i)).toBeTruthy()
  })
})
