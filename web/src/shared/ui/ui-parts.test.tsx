import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import '@/shared/lib/i18n'
import { createNotificationConnection, NOTIFICATION_CREATED } from '@/shared/lib/realtime'
import { useToastStore } from '@/stores/useToastStore'
import { renderRoutes } from '@/test/utils'
import { Alert } from './alert'
import { FormField } from './form-field'
import { Input } from './form-controls'
import { EmptyState, ErrorNotice, PageHeader } from './page-parts'
import { RichTextContent } from './rich-text-content'
import { RichTextEditor } from './rich-text-editor'
import { Toaster } from './toaster'

describe('Alert', () => {
  it('announces an error at once and an information politely', () => {
    const { rerender } = render(<Alert tone="error" message="It failed." />)
    expect(screen.getByRole('alert').textContent).toContain('It failed.')

    rerender(<Alert tone="info" message="FYI." />)
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByText('FYI.')).toBeTruthy()
  })

  it('shows its action next to the message', () => {
    render(<Alert tone="error" message="It failed." action={<button>Reload</button>} />)

    expect(screen.getByRole('button', { name: 'Reload' })).toBeTruthy()
  })
})

describe('page parts', () => {
  it('PageHeader shows the title, the description and the actions', () => {
    render(<PageHeader title="Students" description="Everybody." actions={<button>Add</button>} />)

    expect(screen.getByRole('heading', { level: 1, name: 'Students' })).toBeTruthy()
    expect(screen.getByText('Everybody.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Add' })).toBeTruthy()
  })

  it('EmptyState shows what is missing and what to do', () => {
    render(<EmptyState title="Nothing yet" description="Add one." action={<button>Add</button>} />)

    expect(screen.getByText('Nothing yet')).toBeTruthy()
    expect(screen.getByText('Add one.')).toBeTruthy()
  })

  it('ErrorNotice offers a retry only when it can retry', () => {
    const onRetry = vi.fn()
    const { rerender } = render(<ErrorNotice message="Broken." onRetry={onRetry} />)

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(onRetry).toHaveBeenCalledTimes(1)

    rerender(<ErrorNotice message="Broken." />)
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull()
  })
})

describe('FormField', () => {
  it('wires the label, the hint and the error to the control for screen readers', () => {
    render(
      <FormField label="Email" required hint="Used to sign in." error="Enter a valid email.">
        <Input />
      </FormField>,
    )

    const input = screen.getByLabelText(/Email/)
    expect(input.getAttribute('aria-invalid')).toBe('true')
    const described = input.getAttribute('aria-describedby') ?? ''
    expect(described.split(' ')).toHaveLength(2)
    expect(document.getElementById(described.split(' ')[0]!)?.textContent).toBe('Used to sign in.')
    expect(screen.getByRole('alert').textContent).toBe('Enter a valid email.')
  })

  it('has no error state or description when there is neither', () => {
    render(
      <FormField label="Email">
        <Input />
      </FormField>,
    )

    const input = screen.getByLabelText('Email')
    expect(input.getAttribute('aria-invalid')).toBeNull()
    expect(input.getAttribute('aria-describedby')).toBeNull()
  })
})

describe('RichTextContent', () => {
  it('renders the sanitized HTML it is given', () => {
    render(<RichTextContent html="<p>Hello <strong>world</strong></p>" />)

    expect(screen.getByText('world').tagName).toBe('STRONG')
  })
})

describe('Toaster', () => {
  beforeEach(() => useToastStore.setState({ toasts: [] }))
  afterEach(() => vi.useRealTimers())

  it('shows the toasts and lets the user dismiss one', () => {
    renderRoutes([{ path: '/', element: <Toaster /> }], '/')
    act(() => {
      useToastStore.getState().show({ kind: 'success', title: 'Saved.' })
      useToastStore.getState().show({ kind: 'error', title: 'Failed.', body: 'Try later.' })
    })

    expect(screen.getByText('Saved.')).toBeTruthy()
    expect(screen.getByText('Try later.')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Dismiss' })[0]!)
    expect(screen.queryByText('Saved.')).toBeNull()
  })

  it('goes to the page of a toast with a link when it is clicked, and dismisses it', async () => {
    const { router } = renderRoutes([{ path: '/', element: <Toaster /> }, { path: '/announcements/a1', element: <p>Announcement</p> }], '/')
    act(() => {
      useToastStore.getState().show({ kind: 'info', title: 'New announcement', linkUrl: '/announcements/a1' })
    })

    fireEvent.click(screen.getByRole('button', { name: /New announcement/ }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/announcements/a1'))
    expect(useToastStore.getState().toasts).toEqual([])
  })

  it('removes a toast by itself after a while', () => {
    vi.useFakeTimers()
    renderRoutes([{ path: '/', element: <Toaster /> }], '/')
    act(() => {
      useToastStore.getState().show({ kind: 'info', title: 'Passing by' })
    })

    act(() => {
      vi.advanceTimersByTime(7001)
    })

    expect(screen.queryByText('Passing by')).toBeNull()
  })
})

describe('RichTextEditor', () => {
  it('shows the toolbar and the text, and reports the HTML when the text changes', async () => {
    const onChange = vi.fn()
    render(<RichTextEditor value="<p>Hello</p>" onChange={onChange} label="Article text" />)

    expect(await screen.findByRole('toolbar', { name: 'Text formatting' })).toBeTruthy()
    const editor = screen.getByRole('textbox', { name: 'Article text' })
    expect(editor.textContent).toBe('Hello')

    for (const name of ['Bold', 'Italic', 'Underline', 'Heading', 'Subheading', 'Bulleted list', 'Numbered list', 'Quote', 'Link', 'Undo', 'Redo']) {
      expect(screen.getByRole('button', { name })).toBeTruthy()
    }
  })

  it('has no toolbar and cannot be edited when disabled', async () => {
    render(<RichTextEditor value="<p>Locked</p>" onChange={vi.fn()} label="Article text" disabled />)

    const editor = await screen.findByRole('textbox', { name: 'Article text' })
    expect(editor.getAttribute('contenteditable')).toBe('false')
    expect(screen.queryByRole('toolbar')).toBeNull()
  })

  it('shows new text when the value is replaced from outside', async () => {
    const { rerender } = render(<RichTextEditor value="" onChange={vi.fn()} label="Article text" />)
    await screen.findByRole('textbox', { name: 'Article text' })

    rerender(<RichTextEditor value="<p>Loaded later</p>" onChange={vi.fn()} label="Article text" />)

    await waitFor(() => expect(screen.getByRole('textbox', { name: 'Article text' }).textContent).toBe('Loaded later'))
  })

  it('toggles a format from the toolbar', async () => {
    render(<RichTextEditor value="<p>Hello</p>" onChange={vi.fn()} label="Article text" />)
    const bold = await screen.findByRole('button', { name: 'Bold' })

    fireEvent.click(bold)

    expect(bold.getAttribute('aria-pressed')).toBe('false')
  })
})

describe('createNotificationConnection', () => {
  it('builds a connection to the notification hub of the API', () => {
    const connection = createNotificationConnection(() => 'token')

    expect(connection.baseUrl).toMatch(/\/hubs\/notifications$/)
    expect(NOTIFICATION_CREATED).toBe('NotificationCreated')
  })
})
