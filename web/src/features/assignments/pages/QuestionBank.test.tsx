import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { Question, QuestionInput } from '../api/questions'
import { blankQuestion, isQuestionValid, QuestionEditor } from '../components/QuestionEditor'
import { QuestionBankPage } from './QuestionBankPage'

const api = vi.hoisted(() => ({ listTeachingOptions: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

type Call = { url: string; method: string; data?: unknown; params?: Record<string, unknown> }
const saves = () => http.customInstance.mock.calls.map(([c]) => c as Call).filter((c) => c.method === 'POST' && c.url === '/api/v1/question-bank')
const calls = (method: string, urlPart: string) => http.customInstance.mock.calls.map(([c]) => c as Call).filter((c) => c.method === method && c.url.includes(urlPart))

const question = (overrides: Partial<Question> = {}): Question => ({
  id: 'q1',
  subjectId: 'math',
  type: 'MultipleChoice',
  difficulty: 'Easy',
  body: 'What is 2 + 2?',
  options: ['3', '4', '5'],
  answerKey: '1',
  explanation: 'Because.',
  createdByName: 'Budi Santoso',
  isMine: true,
  ...overrides,
})

const draft = (overrides: Partial<QuestionInput> = {}): QuestionInput => ({
  type: 'MultipleChoice',
  difficulty: 'Easy',
  body: 'Which statement is made?',
  options: ['A', 'B', 'C', 'D'],
  answerKey: '2',
  explanation: 'Stated.',
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

function serve(items: Question[] = [question(), question({ id: 'q2', body: 'Name the capital.', type: 'ShortAnswer', options: [], answerKey: 'Paris', isMine: false, createdByName: 'Siti Aminah', difficulty: 'Hard' })]) {
  http.customInstance.mockImplementation(({ url, method }: Call) => {
    if (method === 'GET' && url === '/api/v1/question-bank') return Promise.resolve(pageOf(items))
    if (method === 'POST' && url === '/api/v1/question-bank') return Promise.resolve([question({ id: 'new' })])
    if (method === 'PUT') return Promise.resolve(question())
    if (method === 'DELETE') return Promise.resolve(undefined)
    if (method === 'POST' && url.endsWith('/generate')) return Promise.resolve({ questions: [draft(), draft({ body: 'Second draft?', answerKey: '0' })], pages: 3, truncated: false })
    return Promise.reject(new Error(`unexpected ${method} ${url}`))
  })
}

const renderBank = () => renderPage(<QuestionBankPage />, '/assignments/questions')

beforeEach(() => {
  api.listTeachingOptions.mockReset()
  http.customInstance.mockReset()
  useToastStore.setState({ toasts: [] })
  api.listTeachingOptions.mockResolvedValue([
    { classroomId: 'c1', classroomName: 'X-A', subjectId: 'math', subjectName: 'Mathematics' },
    { classroomId: 'c2', classroomName: 'X-B', subjectId: 'math', subjectName: 'Mathematics' },
    { classroomId: 'c1', classroomName: 'X-A', subjectId: 'bio', subjectName: 'Biology' },
  ])
  signInAs('Teacher', 'Budi Santoso')
})

describe('the rules of a question', () => {
  it('accepts a complete multiple choice and a complete short answer question', () => {
    expect(isQuestionValid(question())).toBe(true)
    expect(isQuestionValid({ ...blankQuestion('ShortAnswer'), body: 'Why?', answerKey: 'Because' })).toBe(true)
  })

  it.each<[string, Partial<QuestionInput>]>([
    ['an empty text', { body: ' ' }],
    ['one option', { options: ['only'], answerKey: '0' }],
    ['six options', { options: ['1', '2', '3', '4', '5', '6'], answerKey: '0' }],
    ['an empty option', { options: ['a', ' ', 'c'] }],
    ['the same option twice', { options: ['a', 'A', 'c'] }],
    ['a key past the options', { answerKey: '3' }],
    ['a key that is not a position', { answerKey: 'b' }],
    ['a text that is too long', { body: 'q'.repeat(1001) }],
  ])('refuses %s', (_, patch) => {
    expect(isQuestionValid({ ...question(), ...patch })).toBe(false)
  })

  it('refuses a short answer question with options or without a model answer', () => {
    expect(isQuestionValid({ ...blankQuestion('ShortAnswer'), body: 'Why?', answerKey: 'x', options: ['a'] })).toBe(false)
    expect(isQuestionValid({ ...blankQuestion('ShortAnswer'), body: 'Why?', answerKey: ' ' })).toBe(false)
  })
})

describe('QuestionEditor', () => {
  const edit = (value: QuestionInput) => {
    const onChange = vi.fn()
    renderPage(<QuestionEditor value={value} onChange={onChange} label="t" />, '/')
    return onChange
  }

  it('marks the right option by its position', () => {
    const onChange = edit(question())

    expect((screen.getByLabelText('Option 2 is the right answer') as HTMLInputElement).checked).toBe(true)
    fireEvent.click(screen.getByLabelText('Option 3 is the right answer'))

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ answerKey: '2' }))
  })

  it('keeps the right option right when an option before it is removed', () => {
    const onChange = edit(question({ answerKey: '2' }))

    fireEvent.click(screen.getByLabelText('Remove option 1'))

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ options: ['4', '5'], answerKey: '1' }))
  })

  it('moves the mark to the first option when the right one is removed', () => {
    const onChange = edit(question({ answerKey: '1' }))

    fireEvent.click(screen.getByLabelText('Remove option 2'))

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ options: ['3', '5'], answerKey: '0' }))
  })

  it('offers no removal below two options', () => {
    edit(question({ options: ['a', 'b'], answerKey: '0' }))

    expect(screen.queryByLabelText(/^Remove option/)).toBeNull()
  })

  it('offers no addition above five options', () => {
    edit(question({ options: ['a', 'b', 'c', 'd', 'e'] }))

    expect((screen.getByRole('button', { name: 'Add an option' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('shows a model answer instead of options for a short answer question', () => {
    edit({ ...blankQuestion('ShortAnswer'), body: 'Why?' })

    expect(screen.getByLabelText(/^Model answer/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Add an option' })).toBeNull()
  })
})

describe('QuestionBankPage', () => {
  it('lists the questions of the first subject and marks who wrote them', async () => {
    serve()
    renderBank()

    expect(await screen.findByText('What is 2 + 2?')).toBeTruthy()
    expect(screen.getByText('Name the capital.')).toBeTruthy()
    expect(screen.getByText('You')).toBeTruthy()
    expect(screen.getByText('Siti Aminah')).toBeTruthy()
    expect(calls('GET', '/question-bank')[0]!.params).toMatchObject({ subjectId: 'math', page: 1 })
    expect(screen.getAllByRole('option', { name: /Mathematics|Biology/ })).toHaveLength(2) // one entry per subject, not per classroom
  })

  it('offers to change and delete only the questions the teacher wrote', async () => {
    serve()
    renderBank()
    await screen.findByText('What is 2 + 2?')

    expect(screen.getByRole('button', { name: 'Change the question: What is 2 + 2?' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Change the question: Name the capital.' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Delete the question: Name the capital.' })).toBeNull()
  })

  it('asks for the questions of another subject and filters them', async () => {
    serve()
    renderBank()
    await screen.findByText('What is 2 + 2?')

    fireEvent.change(screen.getByLabelText('Subject'), { target: { value: 'bio' } })
    fireEvent.change(screen.getByLabelText('Difficulty'), { target: { value: 'Hard' } })

    await waitFor(() => expect(calls('GET', '/question-bank').at(-1)!.params).toMatchObject({ subjectId: 'bio', difficulty: 'Hard', page: 1 }))
  })

  it('says so when the teacher teaches no subject', async () => {
    api.listTeachingOptions.mockResolvedValue([])
    serve()
    renderBank()

    expect(await screen.findByText('No subjects yet')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Add a question' })).toBeNull()
  })

  it('shows an empty state for a subject without questions', async () => {
    serve([])
    renderBank()

    expect(await screen.findByText('No questions')).toBeTruthy()
  })

  it('adds a question to the bank of the subject', async () => {
    serve()
    renderBank()

    fireEvent.click(await screen.findByRole('button', { name: 'Add a question' }))
    const dialog = await screen.findByRole('dialog')
    const save = within(dialog).getByRole('button', { name: 'Save the question' }) as HTMLButtonElement
    expect(save.disabled).toBe(true)
    fireEvent.change(within(dialog).getByLabelText(/^Question/), { target: { value: 'What is 3 + 3?' } })
    fireEvent.change(within(dialog).getByLabelText('Option 1'), { target: { value: '5' } })
    fireEvent.change(within(dialog).getByLabelText('Option 2'), { target: { value: '6' } })
    fireEvent.click(within(dialog).getByLabelText('Option 2 is the right answer'))
    expect(save.disabled).toBe(false)
    fireEvent.click(save)

    await waitFor(() => expect(toasts()).toContain('success:Question saved.'))
    expect(calls('POST', '/question-bank')[0]!.data).toEqual({
      subjectId: 'math',
      questions: [{ type: 'MultipleChoice', difficulty: 'Easy', body: 'What is 3 + 3?', options: ['5', '6'], answerKey: '1', explanation: '' }],
    })
  })

  it('changes a question the teacher wrote', async () => {
    serve()
    renderBank()

    fireEvent.click(await screen.findByRole('button', { name: 'Change the question: What is 2 + 2?' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/^Question/), { target: { value: 'What is 2 + 3?' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save the question' }))

    await waitFor(() => expect(calls('PUT', '/question-bank/q1')).toHaveLength(1))
    expect((calls('PUT', '/question-bank/q1')[0]!.data as { question: QuestionInput }).question.body).toBe('What is 2 + 3?')
  })

  it('shows why a question could not be saved', async () => {
    serve()
    http.customInstance.mockImplementation(({ url, method }: Call) =>
      method === 'POST' && url === '/api/v1/question-bank' ? Promise.reject(apiError(403, { code: 'questions.not_your_subject' })) : Promise.resolve(pageOf([])),
    )
    renderBank()

    fireEvent.click(await screen.findByRole('button', { name: 'Add a question' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/^Question/), { target: { value: 'Q?' } })
    fireEvent.change(within(dialog).getByLabelText('Option 1'), { target: { value: 'a' } })
    fireEvent.change(within(dialog).getByLabelText('Option 2'), { target: { value: 'b' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save the question' }))

    expect(await within(dialog).findByText('You can only use the question bank of a subject you teach.')).toBeTruthy()
  })

  it('deletes a question after confirmation', async () => {
    serve()
    renderBank()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete the question: What is 2 + 2?' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(calls('DELETE', '/question-bank')).toHaveLength(0)
    fireEvent.click(within(confirm).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(toasts()).toContain('success:Question deleted.'))
    expect(calls('DELETE', '/question-bank/q1')).toHaveLength(1)
  })
})

describe('drafting from a PDF', () => {
  const pdf = (name = 'material.pdf', size = 1000) => {
    const file = new File(['%PDF-1.4'], name, { type: 'application/pdf' })
    Object.defineProperty(file, 'size', { value: size })
    return file
  }

  async function openGenerate() {
    fireEvent.click(await screen.findByRole('button', { name: 'Draft from a PDF' }))
    return screen.findByRole('dialog')
  }

  it('keeps the button off until a PDF is chosen and refuses another kind of file or a big one', async () => {
    serve()
    renderBank()
    const dialog = await openGenerate()
    const submit = within(dialog).getByRole('button', { name: 'Draft the questions' }) as HTMLButtonElement
    expect(submit.disabled).toBe(true)

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf('notes.docx')] } })
    expect(within(dialog).getByText('Choose a PDF file.')).toBeTruthy()
    expect(submit.disabled).toBe(true)

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf('big.pdf', 11 * 1024 * 1024)] } })
    expect(within(dialog).getByText('The PDF is larger than 10 MB.')).toBeTruthy()

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    expect(submit.disabled).toBe(false)
  })

  it('uploads the PDF with the choices and shows the drafts for review', async () => {
    serve()
    renderBank()
    const dialog = await openGenerate()

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    fireEvent.change(within(dialog).getByLabelText(/^Number of questions/), { target: { value: '2' } })
    fireEvent.change(within(dialog).getByLabelText('Difficulty'), { target: { value: 'Hard' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Draft the questions' }))

    expect(await within(dialog).findAllByText('Draft 1')).toBeTruthy()
    const form = calls('POST', '/generate')[0]!.data as FormData
    expect(form).toBeInstanceOf(FormData)
    expect([form.get('subjectId'), form.get('count'), form.get('difficulty'), form.get('type')]).toEqual(['math', '2', 'Hard', 'MultipleChoice'])
    expect((form.get('file') as File).name).toBe('material.pdf')
    expect(within(dialog).getByText(/2 drafts from 3 page\(s\)/)).toBeTruthy()
    expect(saves()).toHaveLength(0) // nothing is saved yet
  })

  it('saves the drafts that were kept, with the edits, and not the discarded ones', async () => {
    serve()
    renderBank()
    const dialog = await openGenerate()
    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Draft the questions' }))
    await within(dialog).findAllByText('Draft 2')

    fireEvent.click(within(dialog).getByRole('button', { name: /Discard.*Draft 2/ }))
    fireEvent.change(within(dialog).getAllByLabelText(/^Question/)[0]!, { target: { value: 'My edited question?' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save 1 to the bank' }))

    await waitFor(() => expect(toasts()).toContain('success:1 questions saved to the bank.'))
    const saved = saves()[0]!.data as { subjectId: string; questions: QuestionInput[] }
    expect(saved.subjectId).toBe('math')
    expect(saved.questions.map((q) => q.body)).toEqual(['My edited question?'])
  })

  it('does not let an invalid draft be saved', async () => {
    serve()
    renderBank()
    const dialog = await openGenerate()
    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Draft the questions' }))
    await within(dialog).findAllByText('Draft 1')

    fireEvent.change(within(dialog).getAllByLabelText(/^Question/)[0]!, { target: { value: ' ' } })

    expect((within(dialog).getByRole('button', { name: 'Save 2 to the bank' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it.each([
    ['questions.pdf_too_long', 'The PDF has more than 30 pages.'],
    ['questions.pdf_no_text', 'The PDF has no text to read; a scanned document cannot be used.'],
    ['ai.daily_limit', 'You have used your AI calls of today. Try again tomorrow.'],
  ])('explains the error %s', async (code, text) => {
    http.customInstance.mockImplementation(({ url, method }: Call) =>
      method === 'POST' && url.endsWith('/generate') ? Promise.reject(apiError(400, { code })) : Promise.resolve(pageOf([])),
    )
    renderBank()
    const dialog = await openGenerate()

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Draft the questions' }))

    expect(await within(dialog).findByText(text)).toBeTruthy()
  })

  it('warns when only the first part of a long PDF was read', async () => {
    serve()
    http.customInstance.mockImplementation(({ url, method }: Call) =>
      method === 'POST' && url.endsWith('/generate') ? Promise.resolve({ questions: [draft()], pages: 30, truncated: true }) : Promise.resolve(pageOf([])),
    )
    renderBank()
    const dialog = await openGenerate()

    fireEvent.change(within(dialog).getByLabelText(/^PDF file/), { target: { files: [pdf()] } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Draft the questions' }))

    expect(await within(dialog).findByText(/the questions come from the first part/)).toBeTruthy()
  })
})
