import { QueryClientProvider } from '@tanstack/react-query'
import { act, render, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { createTestQueryClient, makeToken } from '@/test/utils'
import { RealtimeBridge } from './RealtimeBridge'

type Handler = (push: unknown) => void

const hub = vi.hoisted(() => {
  const state = {
    handlers: new Map<string, Handler>(),
    reconnected: undefined as undefined | (() => void),
    start: vi.fn(),
    stop: vi.fn(),
    created: 0,
    tokenFactory: undefined as undefined | (() => string | null),
  }
  return state
})

vi.mock('@/shared/lib/realtime', () => ({
  NOTIFICATION_CREATED: 'NotificationCreated',
  createNotificationConnection: (tokenFactory: () => string | null) => {
    hub.created++
    hub.tokenFactory = tokenFactory
    return {
      on: (name: string, handler: Handler) => hub.handlers.set(name, handler),
      onreconnected: (handler: () => void) => {
        hub.reconnected = handler
      },
      start: hub.start,
      stop: hub.stop,
    }
  },
}))

function renderBridge() {
  const queryClient = createTestQueryClient()
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const view = render(
    <QueryClientProvider client={queryClient}>
      <RealtimeBridge />
    </QueryClientProvider>,
  )
  return { ...view, invalidate }
}

beforeEach(() => {
  hub.handlers.clear()
  hub.reconnected = undefined
  hub.created = 0
  hub.start.mockReset().mockResolvedValue(undefined)
  hub.stop.mockReset().mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
})

afterEach(() => {
  vi.useRealTimers()
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('RealtimeBridge', () => {
  it('stays disconnected while nobody is signed in', () => {
    useAuthStore.setState({ status: 'anonymous' })

    renderBridge()

    expect(hub.created).toBe(0)
  })

  it('connects once somebody signs in, and hands the connection the current token', () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))

    renderBridge()

    expect(hub.created).toBe(1)
    expect(hub.start).toHaveBeenCalledTimes(1)
    expect(hub.tokenFactory?.()).toBe(useAuthStore.getState().accessToken)
  })

  it('refreshes the notification and announcement queries and shows a toast for a pushed notification', () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))
    const { invalidate } = renderBridge()

    act(() => hub.handlers.get('NotificationCreated')?.({ title: 'New announcement', body: 'School is closed.', linkUrl: '/announcements/a1' }))

    expect(invalidate.mock.calls.map(([filter]) => filter?.queryKey?.[0]).sort()).toEqual(['announcements', 'notifications'])
    expect(useToastStore.getState().toasts).toMatchObject([{ kind: 'info', title: 'New announcement', body: 'School is closed.', linkUrl: '/announcements/a1' }])
  })

  it('catches up on what it missed when the connection comes back', () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))
    const { invalidate } = renderBridge()

    act(() => hub.reconnected?.())

    expect(invalidate).toHaveBeenCalledTimes(2)
    expect(useToastStore.getState().toasts).toEqual([])
  })

  it('retries the first connection until it works', async () => {
    vi.useFakeTimers()
    hub.start.mockRejectedValueOnce(new Error('down')).mockResolvedValue(undefined)
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))

    renderBridge()
    await vi.advanceTimersByTimeAsync(0)
    expect(hub.start).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(5000)
    expect(hub.start).toHaveBeenCalledTimes(2)

    await vi.advanceTimersByTimeAsync(20000)
    expect(hub.start).toHaveBeenCalledTimes(2)
  })

  it('stops retrying and closes the connection when the user signs out', async () => {
    vi.useFakeTimers()
    hub.start.mockRejectedValue(new Error('down'))
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))
    renderBridge()
    await vi.advanceTimersByTimeAsync(0)

    act(() => useAuthStore.getState().clearSession())
    await vi.advanceTimersByTimeAsync(30000)

    expect(hub.stop).toHaveBeenCalledTimes(1)
    expect(hub.start).toHaveBeenCalledTimes(1)
  })

  it('closes the connection when the page goes away', async () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))
    const { unmount } = renderBridge()
    await waitFor(() => expect(hub.start).toHaveBeenCalled())

    unmount()

    expect(hub.stop).toHaveBeenCalledTimes(1)
  })
})
