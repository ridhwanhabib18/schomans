import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderPage } from '@/test/utils'
import { LandingProfile } from './LandingProfile'

describe('LandingProfile', () => {
  it('tells about the school and gives its numbers', () => {
    renderPage(<LandingProfile />)

    expect(screen.getByRole('heading', { name: 'About SchoMans Demo School' })).toBeTruthy()
    expect(screen.getByText(/fictional school/)).toBeTruthy()
    expect(screen.getByText('students').nextElementSibling?.textContent).toBe('150')
    expect(screen.getByText('classes').nextElementSibling?.textContent).toBe('5')
  })
})
