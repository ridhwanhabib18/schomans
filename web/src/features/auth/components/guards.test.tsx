import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { makeToken, renderRoutes } from '@/test/utils'
import { useAuthStore } from '@/stores/useAuthStore'
import { RequireAuth } from './RequireAuth'
import { RequireRole } from './RequireRole'

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

const guardedRoutes = [
  { path: '/login', element: <p>login page</p> },
  { element: <RequireAuth />, children: [{ path: '/secret', element: <p>secret page</p> }] },
]

describe('RequireAuth', () => {
  it('shows a loading state while the session is being checked, and does not redirect yet', () => {
    useAuthStore.setState({ status: 'loading' })

    renderRoutes(guardedRoutes, '/secret')

    expect(screen.getByRole('status')).toHaveProperty('ariaBusy', 'true')
    expect(screen.queryByText('login page')).toBeNull()
    expect(screen.queryByText('secret page')).toBeNull()
  })

  it('sends an anonymous visitor to the login page, remembering where they wanted to go', () => {
    useAuthStore.setState({ status: 'anonymous' })

    const { router } = renderRoutes(guardedRoutes, '/secret')

    expect(screen.getByText('login page')).toBeTruthy()
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.state).toEqual({ from: '/secret' })
  })

  it('lets a signed-in user through', () => {
    useAuthStore.getState().setSession(makeToken({ roles: ['Student'] }))

    renderRoutes(guardedRoutes, '/secret')

    expect(screen.getByText('secret page')).toBeTruthy()
  })
})

describe('RequireRole', () => {
  const routes = [
    {
      path: '/admin-only',
      element: (
        <RequireRole roles={['Admin', 'Principal']}>
          <p>admin area</p>
        </RequireRole>
      ),
    },
  ]

  it.each(['Admin', 'Principal'])('lets %s in', (role) => {
    useAuthStore.getState().setSession(makeToken({ roles: [role] }))

    renderRoutes(routes, '/admin-only')

    expect(screen.getByText('admin area')).toBeTruthy()
  })

  it.each(['Teacher', 'Student', 'Parent', 'Applicant'])('shows a forbidden notice to %s', (role) => {
    useAuthStore.getState().setSession(makeToken({ roles: [role] }))

    renderRoutes(routes, '/admin-only')

    expect(screen.queryByText('admin area')).toBeNull()
    expect(screen.getByRole('alert').textContent).toContain('You do not have access to this page')
  })
})
