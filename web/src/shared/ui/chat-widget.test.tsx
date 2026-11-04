import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderPage } from '@/test/utils'
import { ChatWidget } from './chat-widget'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const known = { known: true, answer: 'Registration opens on 1 March.', sources: [{ title: 'Admissions 2027', url: '/news/admissions-2027' }], contact: null }
const unknown = { known: false, answer: 'I could not find this. Please ask the school staff: demo@schomans.example', sources: [], contact: 'demo@schomans.example' }

const ask = (text: string) => {
  fireEvent.change(screen.getByLabelText('Your question'), { target: { value: text } })
  fireEvent.click(screen.getByRole('button', { name: 'Ask' }))
}

beforeEach(() => {
  http.customInstance.mockReset()
})

describe('ChatWidget', () => {
  it('says whom it answers for and where the answers come from', () => {
    renderPage(<ChatWidget audience="public" />, '/')

    expect(screen.getByRole('heading', { name: 'Ask the school' })).toBeTruthy()
    expect(screen.getByText(/only from the news and announcements the school has published/)).toBeTruthy()
  })

  it('uses the parent wording for parents', () => {
    renderPage(<ChatWidget audience="parent" />, '/')

    expect(screen.getByRole('heading', { name: 'Ask about the school' })).toBeTruthy()
  })

  it('keeps the button off while the question is empty', () => {
    renderPage(<ChatWidget audience="public" />, '/')

    expect((screen.getByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('Your question'), { target: { value: '   ' } })
    expect((screen.getByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('sends the trimmed question to the public endpoint and shows the answer with a link to its source', async () => {
    http.customInstance.mockResolvedValue(known)
    renderPage(<ChatWidget audience="public" />, '/')

    ask('  When does registration open?  ')

    expect(await screen.findByText('Registration opens on 1 March.')).toBeTruthy()
    expect(http.customInstance).toHaveBeenCalledWith({ url: '/api/v1/chatbot/public', method: 'POST', data: { question: 'When does registration open?' } })
    expect(screen.getByText('When does registration open?')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Admissions 2027' }).getAttribute('href')).toBe('/news/admissions-2027')
    expect((screen.getByLabelText('Your question') as HTMLInputElement).value).toBe('')
  })

  it('asks the parent endpoint for a parent', async () => {
    http.customInstance.mockResolvedValue(known)
    renderPage(<ChatWidget audience="parent" />, '/')

    ask('When is the trip?')

    await waitFor(() => expect(http.customInstance).toHaveBeenCalledWith(expect.objectContaining({ url: '/api/v1/chatbot/parent' })))
  })

  it('shows an answer of "I do not know" with the staff contact and no source', async () => {
    http.customInstance.mockResolvedValue(unknown)
    renderPage(<ChatWidget audience="public" />, '/')

    ask('What is the capital of France?')

    expect(await screen.findByText(/Please ask the school staff: demo@schomans.example/)).toBeTruthy()
    expect(screen.queryByText('Source:')).toBeNull()
  })

  it('keeps the conversation in order', async () => {
    http.customInstance.mockResolvedValueOnce(known).mockResolvedValueOnce(unknown)
    renderPage(<ChatWidget audience="public" />, '/')

    ask('First question?')
    await screen.findByText('Registration opens on 1 March.')
    ask('Second question?')
    await screen.findByText(/Please ask the school staff/)

    expect(screen.getAllByRole('listitem').map((li) => li.textContent).join('|')).toMatch(/First question.*Registration opens.*Second question.*Please ask/)
  })

  it('tells a guest who asks too fast to wait', async () => {
    http.customInstance.mockRejectedValue(apiError(429, {}))
    renderPage(<ChatWidget audience="public" />, '/')

    ask('Hello there?')

    expect(await screen.findByText('You are asking too fast. Please wait a minute and try again.')).toBeTruthy()
  })

  it('explains the daily limit of a parent', async () => {
    http.customInstance.mockRejectedValue(apiError(429, { code: 'ai.daily_limit' }))
    renderPage(<ChatWidget audience="parent" />, '/')

    ask('Hello there?')

    expect(await screen.findByText('You have used your AI calls of today. Try again tomorrow.')).toBeTruthy()
  })

  it('falls back to a generic message for an unknown failure', async () => {
    http.customInstance.mockRejectedValue(apiError(500, {}))
    renderPage(<ChatWidget audience="public" />, '/')

    ask('Hello there?')

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeTruthy()
  })
})
