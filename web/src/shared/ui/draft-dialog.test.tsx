import { fireEvent, screen, waitFor } from '@testing-library/react'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { DocumentDraft, DraftDocumentType } from '@/shared/lib/drafts'
import { apiError, renderPage, signInAs } from '@/test/utils'
import { DraftDialog } from './draft-dialog'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const draft: DocumentDraft = { title: 'Open day', paragraphs: ['We invite you.', 'Please come at nine.'] }

function Harness({ type = 'Announcement', replaces, onDraft }: { type?: DraftDocumentType; replaces?: boolean; onDraft: (draft: DocumentDraft) => void }) {
  const [open, setOpen] = useState(true)
  return (
    <>
      <p>{open ? 'dialog open' : 'dialog closed'}</p>
      <DraftDialog open={open} onOpenChange={setOpen} type={type} replaces={replaces} onDraft={onDraft} />
    </>
  )
}

const fill = async (audience = 'Parents of class 7A', notes = 'Open day on Saturday.') => {
  fireEvent.change(await screen.findByLabelText(/^Who is it for/), { target: { value: audience } })
  fireEvent.change(screen.getByLabelText(/^Short notes/), { target: { value: notes } })
}

beforeEach(() => {
  http.customInstance.mockReset()
  signInAs('Admin')
})

describe('DraftDialog', () => {
  it('names the kind of document and keeps the button off until the audience and the notes are given', async () => {
    renderPage(<Harness type="Invitation" onDraft={vi.fn()} />, '/')

    expect(await screen.findByRole('heading', { name: 'Draft the invitation with AI' })).toBeTruthy()
    const write = screen.getByRole('button', { name: 'Write a draft' }) as HTMLButtonElement
    expect(write.disabled).toBe(true)

    fireEvent.change(screen.getByLabelText(/^Who is it for/), { target: { value: 'Parents' } })
    expect(write.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText(/^Short notes/), { target: { value: '  ' } })
    expect(write.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText(/^Short notes/), { target: { value: 'Open day.' } })
    expect(write.disabled).toBe(false)
  })

  it('asks for the draft with the trimmed audience and notes, hands it over and closes', async () => {
    http.customInstance.mockResolvedValue(draft)
    const onDraft = vi.fn()
    renderPage(<Harness type="Notice" onDraft={onDraft} />, '/')

    await fill('  Parents of class 7A  ', '  Open day on Saturday.  ')
    fireEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    await waitFor(() => expect(onDraft).toHaveBeenCalledWith(draft))
    expect(http.customInstance).toHaveBeenCalledWith({ url: '/api/v1/drafts', method: 'POST', data: { type: 'Notice', audience: 'Parents of class 7A', notes: 'Open day on Saturday.' } })
    expect(await screen.findByText('dialog closed')).toBeTruthy()
  })

  it('says why the AI could not write and stays open with what the person typed', async () => {
    http.customInstance.mockRejectedValue(apiError(429, { code: 'ai.daily_limit' }))
    const onDraft = vi.fn()
    renderPage(<Harness onDraft={onDraft} />, '/')

    await fill()
    fireEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(await screen.findByText('You have used your AI calls of today. Try again tomorrow.')).toBeTruthy()
    expect(onDraft).not.toHaveBeenCalled()
    expect(screen.getByText('dialog open')).toBeTruthy()
    expect((screen.getByLabelText(/^Short notes/) as HTMLTextAreaElement).value).toBe('Open day on Saturday.')
  })

  it('falls back to the title of an error it has no text for', async () => {
    http.customInstance.mockRejectedValue(apiError(500, { title: 'The server is busy.' }))
    renderPage(<Harness onDraft={vi.fn()} />, '/')

    await fill()
    fireEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(await screen.findByText('The server is busy.')).toBeTruthy()
  })

  it('does not submit the form of the editor that renders it', async () => {
    http.customInstance.mockResolvedValue(draft)
    const submitted = vi.fn()
    renderPage(
      <form onSubmit={(event) => { event.preventDefault(); submitted() }}>
        <Harness onDraft={vi.fn()} />
      </form>,
      '/',
    )

    await fill()
    fireEvent.click(screen.getByRole('button', { name: 'Write a draft' }))

    expect(await screen.findByText('dialog closed')).toBeTruthy()
    expect(submitted).not.toHaveBeenCalled()
  })

  it('warns when the draft replaces text that is already there', async () => {
    renderPage(<Harness replaces onDraft={vi.fn()} />, '/')

    expect(await screen.findByText('The draft replaces the title and the text you have now.')).toBeTruthy()
  })

  it('does not warn when there is nothing to replace', async () => {
    renderPage(<Harness onDraft={vi.fn()} />, '/')

    await screen.findByRole('heading', { name: 'Draft the announcement with AI' })
    expect(screen.queryByText('The draft replaces the title and the text you have now.')).toBeNull()
  })
})
