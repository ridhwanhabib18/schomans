import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderRoutes } from '@/test/utils'
import { NotificationBell } from './NotificationBell'

const api = vi.hoisted(() => ({
  getUnreadNotificationCount: vi.fn(),
  listMyNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const notification = (overrides: Record<string, unknown> = {}) => ({
  id: 'n1',
  title: 'New announcement',
  body: 'School is closed on Friday.',
  linkUrl: '/announcements/a1',
  isRead: false,
  createdAt: '2026-10-27T08:00:00Z',
  ...overrides,
})

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 10, totalCount: items.length })

function renderBell() {
  return renderRoutes(
    [
      { path: '/', element: <NotificationBell /> },
      { path: '/announcements/:id', element: <p>Announcement opened</p> },
    ],
    '/',
  )
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.getUnreadNotificationCount.mockResolvedValue({ count: 0 })
  api.listMyNotifications.mockResolvedValue(page([]))
  api.markNotificationRead.mockResolvedValue(undefined)
  api.markAllNotificationsRead.mockResolvedValue(undefined)
})

describe('NotificationBell', () => {
  it('shows no badge when everything is read', async () => {
    renderBell()

    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeTruthy()
    expect(screen.queryByTestId('unread-badge')).toBeNull()
  })

  it('shows the unread count in the badge and in the accessible name', async () => {
    api.getUnreadNotificationCount.mockResolvedValue({ count: 3 })

    renderBell()

    expect((await screen.findByTestId('unread-badge')).textContent).toBe('3')
    expect(screen.getByRole('button', { name: 'Notifications, 3 unread' })).toBeTruthy()
  })

  it('caps the badge at 99+', async () => {
    api.getUnreadNotificationCount.mockResolvedValue({ count: 250 })

    renderBell()

    expect((await screen.findByTestId('unread-badge')).textContent).toBe('99+')
  })

  it('opens a panel with the latest notifications and closes it with Escape', async () => {
    api.listMyNotifications.mockResolvedValue(page([notification()]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))

    expect(await screen.findByText('School is closed on Friday.')).toBeTruthy()
    expect(screen.getByRole('region', { name: 'Notifications' })).toBeTruthy()

    fireEvent.keyDown(document, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Notifications' })).toBeNull())
  })

  it('says so when there is nothing yet', async () => {
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))

    expect(await screen.findByText('You have no notifications yet.')).toBeTruthy()
  })

  it('marks an unread notification read and goes to its page when it is clicked', async () => {
    api.getUnreadNotificationCount.mockResolvedValue({ count: 1 })
    api.listMyNotifications.mockResolvedValue(page([notification()]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }))
    fireEvent.click(await screen.findByRole('button', { name: /New announcement/ }))

    await waitFor(() => expect(screen.getByText('Announcement opened')).toBeTruthy())
    expect(api.markNotificationRead).toHaveBeenCalledWith('n1')
  })

  it('does not mark a read notification again', async () => {
    api.listMyNotifications.mockResolvedValue(page([notification({ isRead: true })]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))
    fireEvent.click(await screen.findByRole('button', { name: /New announcement/ }))

    await waitFor(() => expect(screen.getByText('Announcement opened')).toBeTruthy())
    expect(api.markNotificationRead).not.toHaveBeenCalled()
  })

  it('marks everything read, and offers that only while something is unread', async () => {
    api.getUnreadNotificationCount.mockResolvedValue({ count: 2 })
    api.listMyNotifications.mockResolvedValue(page([notification(), notification({ id: 'n2' })]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 2 unread' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Mark all as read' }))

    await waitFor(() => expect(api.markAllNotificationsRead).toHaveBeenCalledTimes(1))
  })

  it('disables "mark all as read" when nothing is unread', async () => {
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))

    expect((await screen.findByRole('button', { name: 'Mark all as read' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('offers a retry when the list cannot be loaded', async () => {
    api.listMyNotifications.mockRejectedValueOnce(new Error('down')).mockResolvedValue(page([notification()]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))
    expect(await screen.findByText('Notifications could not be loaded.')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('School is closed on Friday.')).toBeTruthy()
  })
})
