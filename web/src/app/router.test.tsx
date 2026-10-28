import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeToken, renderRoutes } from '@/test/utils'
import { useAuthStore } from '@/stores/useAuthStore'
import { routes } from './router'

const api = vi.hoisted(() => ({
  getDashboard: vi.fn(),
  listPublicNews: vi.fn(),
  listPublicAnnouncements: vi.fn(),
  getUnreadNotificationCount: vi.fn(),
  listMyNotifications: vi.fn(),
  listMyChildren: vi.fn(),
}))
const { getDashboard, listPublicNews, listPublicAnnouncements } = api

vi.mock('@/api/generated/schomans', () => api)
vi.mock('@/shared/lib/config', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/shared/lib/config')>()
  return { ...original, config: { ...original.config, demoMode: true } }
})

const emptyPage = { items: [], page: 1, pageSize: 10, totalCount: 0 }

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  listPublicNews.mockResolvedValue(emptyPage)
  listPublicAnnouncements.mockResolvedValue(emptyPage)
  api.getUnreadNotificationCount.mockResolvedValue({ count: 0 })
  api.listMyNotifications.mockResolvedValue(emptyPage)
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('app routes', () => {
  it('shows the public landing page to guests', () => {
    useAuthStore.setState({ status: 'anonymous' })

    renderRoutes(routes, '/')

    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('School management in one place')
    // One sign-in link in the header and one in the hero.
    const signIn = screen.getAllByRole('link', { name: 'Sign in' })
    expect(signIn.length).toBe(2)
    signIn.forEach((link) => expect(link.getAttribute('href')).toBe('/login'))
  })

  it('shows the latest news and the public announcements on the landing page', async () => {
    useAuthStore.setState({ status: 'anonymous' })
    listPublicNews.mockResolvedValue({
      ...emptyPage,
      items: [{ id: 'n1', slug: 'open-house', title: 'Open house', excerpt: 'Come and see.', category: 'News', coverImageUrl: null, publishedAt: '2026-10-01T08:00:00Z' }],
      totalCount: 1,
    })
    listPublicAnnouncements.mockResolvedValue({
      ...emptyPage,
      items: [{ id: 'a1', title: 'Holiday', excerpt: 'School is closed.', coverImageUrl: null, publishedAt: '2026-10-02T08:00:00Z', isRead: false }],
      totalCount: 1,
    })

    renderRoutes(routes, '/')

    expect(await screen.findByText('Open house')).toBeTruthy()
    expect(screen.getByRole('link', { name: /Holiday/ }).getAttribute('href')).toBe('/announcements/public/a1')
  })

  it('opens the public news list without signing in', async () => {
    useAuthStore.setState({ status: 'anonymous' })

    const { router } = renderRoutes(routes, '/news')

    expect(await screen.findByRole('heading', { level: 1, name: 'School news' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/news')
  })

  it.each([
    ['Teacher', '/manage/teachers'],
    ['Teacher', '/manage/news'],
    ['Teacher', '/manage/announcements/new'],
    ['Student', '/manage/students'],
    ['Parent', '/manage/academic-years'],
    ['Principal', '/manage/academic-years/rollover'],
    ['Student', '/attendance'],
    ['Teacher', '/attendance/scan'],
    ['Parent', '/attendance/reports'],
    ['Admin', '/assignments'],
    ['Student', '/assignments/new'],
    ['Teacher', '/my-children/assignments'],
    ['Student', '/manage/schedules'],
  ])('tells a %s who opens %s that the page is not for their role', async (role, path) => {
    useAuthStore.getState().setSession(makeToken({ roles: [role] }))

    renderRoutes(routes, path)

    expect((await screen.findByRole('alert')).textContent).toContain('You do not have access to this page')
  })

  it('shows the admin the management entries in the navigation, grouped under their sections', async () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Admin'], name: 'Sari Dewi' }))
    getDashboard.mockResolvedValue({ role: 'Admin', fullName: 'Sari Dewi', sections: [] })

    renderRoutes(routes, '/dashboard')

    await waitFor(() => expect(screen.getAllByRole('link', { name: 'Manage news' }).length).toBeGreaterThan(0))
    for (const name of ['Students', 'Teachers', 'Classrooms', 'Subjects', 'Academic years', 'Announcements', 'Manage announcements']) {
      expect(screen.getAllByRole('link', { name }).length).toBeGreaterThan(0)
    }
  })

  it.each([
    ['Teacher', ['Attendance', 'Attendance reports', 'Assignments', 'Timetable']],
    ['Student', ['Scan QR', 'My attendance', 'Assignments']],
    ['Parent', ['Attendance', 'Assignments']],
    ['Principal', ['Attendance reports', 'Assignments', 'Timetable']],
  ])('shows a %s the classroom entries of their role', async (role, names) => {
    useAuthStore.getState().setSession(makeToken({ roles: [role] }))
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderRoutes(routes, '/dashboard')

    await waitFor(() => expect(screen.getAllByRole('link', { name: 'Announcements' }).length).toBeGreaterThan(0))
    for (const name of names) {
      expect(screen.getAllByRole('link', { name }).length, name).toBeGreaterThan(0)
    }
  })

  it('keeps the attendance code through the sign-in of a student who scanned it before logging in', async () => {
    useAuthStore.setState({ status: 'anonymous' })

    const { router } = renderRoutes(routes, '/attend?t=0123456789abcdef0123456789abcdef.abc')

    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.state).toEqual({ from: '/attend?t=0123456789abcdef0123456789abcdef.abc' })
  })

  it('shows a student only the dashboard and the announcements', async () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Student'] }))
    getDashboard.mockResolvedValue({ role: 'Student', fullName: 'Rina Putri', sections: [] })

    renderRoutes(routes, '/dashboard')

    await waitFor(() => expect(screen.getAllByRole('link', { name: 'Announcements' }).length).toBeGreaterThan(0))
    expect(screen.queryByRole('link', { name: 'Students' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Manage news' })).toBeNull()
  })

  it('sends an anonymous visitor who opens the dashboard to the login page with the demo buttons', () => {
    useAuthStore.setState({ status: 'anonymous' })

    const { router } = renderRoutes(routes, '/dashboard')

    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByRole('heading', { name: 'Sign in to SchoMans' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Try the demo' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Browse as guest' }).getAttribute('href')).toBe('/')
  })

  it.each([
    ['Principal', 'Principal dashboard'],
    ['Teacher', 'Teacher dashboard'],
    ['Student', 'Student dashboard'],
    ['Parent', 'Parent dashboard'],
    ['Admin', 'Admin dashboard'],
    ['Applicant', 'Applicant dashboard'],
  ])('shows a signed-in %s their own dashboard inside the app shell', async (role, title) => {
    useAuthStore.getState().setSession(makeToken({ roles: [role], name: 'Rina Putri' }))
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderRoutes(routes, '/dashboard')

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(title))
    // The shell shows who is signed in, the navigation, and a way out.
    expect(screen.getAllByText('Rina Putri').length).toBeGreaterThan(0)
    expect(screen.getAllByRole('navigation', { name: 'Main navigation' }).length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeTruthy()
  })

  it('redirects a signed-in user who opens the login page to the dashboard', async () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Teacher'] }))
    getDashboard.mockResolvedValue({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })

    const { router } = renderRoutes(routes, '/login')

    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
  })

  it('shows a not-found page for an unknown address', () => {
    useAuthStore.setState({ status: 'anonymous' })

    renderRoutes(routes, '/no/such/page')

    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeTruthy()
  })
})
