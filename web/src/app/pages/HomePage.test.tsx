import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { renderPage } from '@/test/utils'
import { HomePage } from './HomePage'

// The sections of the landing page have their own tests; here only the chatbot is looked at.
vi.mock('@/features/admissions/components/AdmissionsBanner', () => ({ AdmissionsBanner: () => null }))
vi.mock('@/features/announcements/components/PublicAnnouncementsSection', () => ({ PublicAnnouncementsSection: () => null }))
vi.mock('@/features/landing/components/LandingHero', () => ({ LandingHero: () => null }))
vi.mock('@/features/landing/components/LandingProfile', () => ({ LandingProfile: () => null }))
vi.mock('@/features/news/components/AwardsSection', () => ({ AwardsSection: () => null }))
vi.mock('@/features/news/components/LatestNews', () => ({ LatestNews: () => null }))

describe('HomePage', () => {
  it('offers every visitor the chatbot of the school', async () => {
    renderPage(<HomePage />, '/')

    expect(await screen.findByRole('heading', { name: 'Ask the school' })).toBeTruthy()
    expect(screen.getByLabelText('Your question')).toBeTruthy()
  })
})
