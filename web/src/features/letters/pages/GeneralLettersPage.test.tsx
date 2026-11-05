import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { GeneralLetter } from '../api/letters'
import { bodyProblem, GeneralLettersPage, toParagraphs } from './GeneralLettersPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const letter = (overrides: Partial<GeneralLetter> = {}): GeneralLetter => ({
  id: 'l1',
  kind: 'Invitation',
  letterNumber: '001/UND/SCHOMANS/XI/2026',
  recipient: 'Parents of class 7A',
  subject: 'Open day',
  signedByName: 'M Ridhwan Habib A',
  issuedAt: '2026-11-04T02:00:00Z',
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)
const posts = (urlPart: string) =>
  http.customInstance.mock.calls.map(([c]) => c as { url: string; method: string; data?: Record<string, unknown> }).filter((c) => c.method === 'POST' && c.url.includes(urlPart))

function serve(items: GeneralLetter[] = [letter()]) {
  http.customInstance.mockImplementation(({ url, method }: { url: string; method: string }) => {
    if (method === 'GET' && url === '/api/v1/general-letters') return Promise.resolve({ items, page: 1, pageSize: 20, totalCount: items.length })
    if (method === 'GET' && url.endsWith('/url')) return Promise.resolve({ url: 'https://storage.test/letter.pdf', expiresAt: '2026-11-04T02:05:00Z', letterNumber: 'x' })
    if (method === 'POST' && url === '/api/v1/general-letters') return Promise.resolve(letter({ id: 'l2', letterNumber: '002/UND/SCHOMANS/XI/2026' }))
    if (method === 'POST' && url === '/api/v1/drafts') return Promise.resolve({ title: 'Open day invitation', paragraphs: ['We invite you.', 'Please come at nine.'] })
    return Promise.reject(new Error(`unexpected ${method} ${url}`))
  })
}

const fillLetter = async () => {
  fireEvent.change(await screen.findByLabelText(/^To/), { target: { value: 'All parents' } })
  fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'Holiday' } })
  fireEvent.change(screen.getByLabelText(/^Text of the letter/), { target: { value: 'School is closed on Monday.\n\nSee you on Tuesday.' } })
}

beforeEach(() => {
  http.customInstance.mockReset()
  useToastStore.setState({ toasts: [] })
  signInAs('Admin', 'M Ridhwan Habib A')
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('the text of a letter', () => {
  it('is split into paragraphs at blank lines, trimmed, without empty ones', () => {
    expect(toParagraphs('One.\n\n  Two.  \n   \n\n\nThree.\nstill three.')).toEqual(['One.', 'Two.', 'Three.\nstill three.'])
    expect(toParagraphs('  \n\n ')).toEqual([])
  })

  it('has limits that match the API', () => {
    expect(bodyProblem(['One.'])).toBeNull()
    expect(bodyProblem(Array.from({ length: 9 }, () => 'x'))).toBe('tooManyParagraphs')
    expect(bodyProblem(['x'.repeat(801)])).toBe('paragraphTooLong')
    expect(bodyProblem(Array.from({ length: 6 }, () => 'x'.repeat(800)))).toBe('tooLong')
  })
})

describe('GeneralLettersPage', () => {
  it('lists the letters issued so far', async () => {
    serve([letter(), letter({ id: 'l0', kind: 'Notice', letterNumber: '001/PEM/SCHOMANS/X/2026', subject: 'Holiday notice' })])
    renderPage(<GeneralLettersPage />, '/manage/letters')

    expect(await screen.findByText('001/UND/SCHOMANS/XI/2026')).toBeTruthy()
    expect(screen.getByText('Holiday notice')).toBeTruthy()
    expect(screen.getAllByText('M Ridhwan Habib A').length).toBeGreaterThan(0)
  })

  it('shows an empty state when no letter was issued', async () => {
    serve([])
    renderPage(<GeneralLettersPage />, '/manage/letters')

    expect(await screen.findByText('No letters yet')).toBeTruthy()
  })

  it('offers a retry when the list cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(500, {}))
    renderPage(<GeneralLettersPage />, '/manage/letters')

    expect(await screen.findByText('The letters could not be loaded.')).toBeTruthy()
  })

  it('opens the PDF of a letter in a new tab', async () => {
    serve()
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    renderPage(<GeneralLettersPage />, '/manage/letters')

    fireEvent.click(await screen.findByRole('button', { name: 'Open the PDF of letter 001/UND/SCHOMANS/XI/2026' }))

    await waitFor(() => expect(open).toHaveBeenCalledWith('https://storage.test/letter.pdf', '_blank', 'noopener,noreferrer'))
  })

  it('keeps the issue button off until the letter is complete', async () => {
    serve()
    renderPage(<GeneralLettersPage />, '/manage/letters')
    const issue = (await screen.findByRole('button', { name: 'Issue the letter' })) as HTMLButtonElement
    expect(issue.disabled).toBe(true)

    await fillLetter()
    expect(issue.disabled).toBe(false)

    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: ' ' } })
    expect(issue.disabled).toBe(true)
  })

  it('refuses a text with too many paragraphs and says so', async () => {
    serve()
    renderPage(<GeneralLettersPage />, '/manage/letters')
    await fillLetter()

    fireEvent.change(screen.getByLabelText(/^Text of the letter/), { target: { value: Array.from({ length: 9 }, (_, i) => `Paragraph ${i}`).join('\n\n') } })

    expect(screen.getByText('A letter has at most 8 paragraphs.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Issue the letter' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('puts the AI draft into the form for the person to change, and issues nothing by itself', async () => {
    serve()
    renderPage(<GeneralLettersPage />, '/manage/letters')

    fireEvent.click(await screen.findByRole('button', { name: 'Draft with AI' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('heading', { name: 'Draft the invitation with AI' })).toBeTruthy()
    fireEvent.change(within(dialog).getByLabelText(/^Who is it for/), { target: { value: 'Parents' } })
    fireEvent.change(within(dialog).getByLabelText(/^Short notes/), { target: { value: 'Open day on Saturday.' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Write a draft' }))

    await waitFor(() => expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('Open day invitation'))
    expect((screen.getByLabelText(/^Text of the letter/) as HTMLTextAreaElement).value).toBe('We invite you.\n\nPlease come at nine.')
    expect(posts('/api/v1/general-letters')).toHaveLength(0)
  })

  it('drafts a notice when the kind is a notice', async () => {
    serve()
    renderPage(<GeneralLettersPage />, '/manage/letters')

    fireEvent.change(await screen.findByLabelText('Kind of letter'), { target: { value: 'Notice' } })
    fireEvent.click(screen.getByRole('button', { name: 'Draft with AI' }))

    expect(await screen.findByRole('heading', { name: 'Draft the notice with AI' })).toBeTruthy()
  })

  it('asks to confirm, then issues the paragraphs, announces the number and clears the form', async () => {
    serve()
    renderPage(<GeneralLettersPage />, '/manage/letters')
    await fillLetter()

    fireEvent.click(screen.getByRole('button', { name: 'Issue the letter' }))
    const confirm = await screen.findByRole('alertdialog')
    expect(posts('/api/v1/general-letters')).toHaveLength(0)
    fireEvent.click(within(confirm).getByRole('button', { name: 'Issue the letter' }))

    await waitFor(() => expect(toasts()).toContain('success:Letter 002/UND/SCHOMANS/XI/2026 issued.'))
    expect(posts('/api/v1/general-letters')[0]!.data).toEqual({
      kind: 'Invitation',
      recipient: 'All parents',
      subject: 'Holiday',
      paragraphs: ['School is closed on Monday.', 'See you on Tuesday.'],
    })
    expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('')
  })

  it('shows why a letter could not be issued and keeps the text', async () => {
    serve()
    http.customInstance.mockImplementation(({ url, method }: { url: string; method: string }) =>
      method === 'POST' && url === '/api/v1/general-letters'
        ? Promise.reject(apiError(409, { code: 'general_letters.number_conflict' }))
        : Promise.resolve({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
    )
    renderPage(<GeneralLettersPage />, '/manage/letters')
    await fillLetter()

    fireEvent.click(screen.getByRole('button', { name: 'Issue the letter' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Issue the letter' }))

    expect(await screen.findByText('The letter could not be numbered. Try again.')).toBeTruthy()
    expect((screen.getByLabelText(/^Subject/) as HTMLInputElement).value).toBe('Holiday')
  })
})
