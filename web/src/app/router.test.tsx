import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeToken, renderRoutes } from '@/test/utils'
import { useAuthStore } from '@/stores/useAuthStore'
import { routes } from './router'

const { getDashboard } = vi.hoisted(() => ({ getDashboard: vi.fn() }))

vi.mock('@/api/generated/schomans', () => ({ getDashboard }))
vi.mock('@/shared/lib/config', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/shared/lib/config')>()
  return { ...original, config: { ...original.config, demoMode: true } }
})

beforeEach(() => {
  getDashboard.mockReset()
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('app routes', () => {
  it('shows the public landing page to guests', () => {
    useAuthStore.setState({ status: 'anonymous' })

    renderRoutes(routes, '/')

    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('School management in one place')
    expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/login')
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
