import { fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { APP_ROLES } from '@/shared/lib/roles'
import { renderRoutes } from '@/test/utils'
import { DemoLoginButtons } from './DemoLoginButtons'

const { demoLogin } = vi.hoisted(() => ({ demoLogin: vi.fn() }))

vi.mock('../api/authService', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/authService')>()),
  demoLogin,
}))

const routes = [
  { path: '/login', element: <DemoLoginButtons returnTo="/dashboard" /> },
  { path: '/dashboard', element: <p>dashboard page</p> },
]

beforeEach(() => {
  demoLogin.mockReset()
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('DemoLoginButtons', () => {
  it('offers one button for each of the six roles', () => {
    renderRoutes(routes, '/login')

    expect(screen.getAllByRole('button')).toHaveLength(6)
    for (const label of ['Principal', 'Teacher', 'Student', 'Parent', 'Admin staff', 'Applicant']) {
      expect(screen.getByRole('button', { name: new RegExp(`^${label}`) })).toBeTruthy()
    }
  })

  it.each(APP_ROLES)('signs in as %s and continues to the dashboard', async (role) => {
    demoLogin.mockResolvedValue(undefined)
    renderRoutes(routes, '/login')
    const label = role === 'Admin' ? 'Admin staff' : role

    fireEvent.click(screen.getByRole('button', { name: new RegExp(`^${label}`) }))

    await waitFor(() => expect(screen.getByText('dashboard page')).toBeTruthy())
    expect(demoLogin).toHaveBeenCalledWith(role)
  })

  it('shows an error and stays on the page when the demo login fails', async () => {
    demoLogin.mockRejectedValue(new Error('keycloak is down'))
    renderRoutes(routes, '/login')

    fireEvent.click(screen.getByRole('button', { name: /^Teacher/ }))

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Demo sign-in failed'))
    expect(screen.queryByText('dashboard page')).toBeNull()
  })
})
