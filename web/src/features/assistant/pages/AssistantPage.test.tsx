import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { AssistantAnswer } from '../api/types'
import { AssistantPage } from './AssistantPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const ANSWER: AssistantAnswer = {
  answer: 'Here is what I found. The attendance rate is 92% over 120 records.',
  sources: [
    {
      tool: 'attendance_by_classroom',
      label: 'Attendance per classroom',
      url: '/attendance/reports',
      figures: [
        { label: 'Period', value: '2026-10-04 to 2026-11-03' },
        { label: 'Attendance rate', value: '92%' },
      ],
    },
    { tool: 'admission_applicants', label: 'Admission applicants', url: null, figures: [{ label: 'Applicants', value: '12' }] },
  ],
}

const calls = () => http.customInstance.mock.calls.map(([c]) => c).filter((c) => c.url === '/api/v1/assistant/ask')

beforeEach(() => {
  http.customInstance.mockReset()
  http.customInstance.mockResolvedValue(ANSWER)
  signInAs('Principal', 'Dewi Lestari')
})

describe('AssistantPage', () => {
  it('answers an example question and shows the figures it rests on, with a link to the report page', async () => {
    renderPage(<AssistantPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'How is the attendance this month?' }))

    expect(await screen.findByText(ANSWER.answer)).toBeTruthy()
    expect(calls()[0]?.data).toEqual({ question: 'How is the attendance this month?', history: [] })
    expect(screen.getByText('Where the numbers come from')).toBeTruthy()
    expect(screen.getByText('92%')).toBeTruthy()
    const link = screen.getByRole('link', { name: /Open the report/ })
    expect(link.getAttribute('href')).toBe('/attendance/reports')
    // a source without a page of its own shows its figures but no link
    expect(screen.getAllByRole('link')).toHaveLength(1)
  })

  it('sends the earlier turns with a follow-up question', async () => {
    renderPage(<AssistantPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'How is the attendance this month?' }))
    await screen.findByText(ANSWER.answer)

    fireEvent.change(screen.getByLabelText('Your question'), { target: { value: 'And in class 8B?' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    await waitFor(() => expect(calls()).toHaveLength(2))
    expect(calls()[1]?.data).toEqual({
      question: 'And in class 8B?',
      history: [
        { role: 'user', text: 'How is the attendance this month?' },
        { role: 'assistant', text: ANSWER.answer },
      ],
    })
  })

  it('sends at most the last ten turns', async () => {
    renderPage(<AssistantPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'How is the attendance this month?' }))
    await screen.findByText(ANSWER.answer)

    for (let i = 0; i < 7; i++) {
      fireEvent.change(screen.getByLabelText('Your question'), { target: { value: `Question ${i}` } })
      fireEvent.click(screen.getByRole('button', { name: 'Ask' }))
      await waitFor(() => expect(calls()).toHaveLength(i + 2))
      await waitFor(() => expect((screen.getByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)) // empty field again
    }

    expect((calls().at(-1)?.data as { history: unknown[] }).history).toHaveLength(10)
  })

  it('does not send an empty question', async () => {
    renderPage(<AssistantPage />)

    expect((await screen.findByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('Your question'), { target: { value: '   ' } })

    expect((screen.getByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)
    expect(calls()).toHaveLength(0)
  })

  it('sends the question when Enter is pressed, and keeps Shift+Enter for a new line', async () => {
    renderPage(<AssistantPage />)
    const field = await screen.findByLabelText('Your question')

    fireEvent.change(field, { target: { value: 'Pending requests?' } })
    fireEvent.keyDown(field, { key: 'Enter', shiftKey: true })
    expect(calls()).toHaveLength(0)
    fireEvent.keyDown(field, { key: 'Enter' })

    await waitFor(() => expect(calls()).toHaveLength(1))
    expect((field as HTMLTextAreaElement).value).toBe('')
  })

  it('says why the assistant cannot answer', async () => {
    http.customInstance.mockRejectedValue(apiError(429, { code: 'ai.daily_limit' }))
    renderPage(<AssistantPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Which requests are waiting for approval?' }))

    expect(await screen.findByText('You have used your AI calls of today. Try again tomorrow.')).toBeTruthy()
  })

  it('starts over with a new conversation', async () => {
    renderPage(<AssistantPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'How is the attendance this month?' }))
    await screen.findByText(ANSWER.answer)

    fireEvent.click(screen.getByRole('button', { name: 'New conversation' }))

    expect(screen.queryByText(ANSWER.answer)).toBeNull()
    expect(screen.getByText('Try asking')).toBeTruthy()
  })
})
