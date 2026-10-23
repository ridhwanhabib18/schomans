import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderWithQuery } from '@/test/utils'
import { DashboardPage } from './DashboardPage'

const { getDashboard } = vi.hoisted(() => ({ getDashboard: vi.fn() }))

vi.mock('@/api/generated/schomans', () => ({ getDashboard }))

beforeEach(() => {
  getDashboard.mockReset()
})

describe('DashboardPage', () => {
  it('shows a skeleton while loading', () => {
    getDashboard.mockReturnValue(new Promise(() => {}))

    renderWithQuery(<DashboardPage />)

    expect(screen.getByRole('status')).toHaveProperty('ariaBusy', 'true')
  })

  it.each([
    ['Principal', 'Principal dashboard'],
    ['Teacher', 'Teacher dashboard'],
    ['Student', 'Student dashboard'],
    ['Parent', 'Parent dashboard'],
    ['Admin', 'Admin dashboard'],
    ['Applicant', 'Applicant dashboard'],
  ])('shows the empty %s dashboard', async (role, title) => {
    getDashboard.mockResolvedValue({ role, fullName: 'Rina Putri', sections: [] })

    renderWithQuery(<DashboardPage />)

    expect((await screen.findByRole('heading', { level: 1 })).textContent).toBe(title)
    expect(screen.getByText('Welcome, Rina Putri')).toBeTruthy()
    expect(screen.getByText('Nothing to show yet')).toBeTruthy()
  })

  it('shows an error with a retry button when the API call fails, and recovers', async () => {
    getDashboard.mockRejectedValueOnce(new Error('boom'))
    getDashboard.mockResolvedValueOnce({ role: 'Teacher', fullName: 'Budi Santoso', sections: [] })

    renderWithQuery(<DashboardPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('The dashboard could not be loaded.')
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Teacher dashboard'))
  })
})
