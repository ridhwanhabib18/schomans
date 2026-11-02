import { screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, pageOf, renderPage } from '@/test/utils'
import { AwardsSection } from './AwardsSection'

const api = vi.hoisted(() => ({ listPublicNews: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const article = (slug: string, title: string) => ({
  slug,
  title,
  excerpt: 'Our team won.',
  coverImageUrl: null,
  category: 'Award',
  publishedAt: '2026-10-10T03:00:00Z',
})

beforeEach(() => api.listPublicNews.mockReset())

describe('AwardsSection', () => {
  it('shows the newest published articles of the category Award', async () => {
    api.listPublicNews.mockResolvedValue(pageOf([article('math-olympiad', 'Math olympiad gold'), article('robotics', 'Robotics cup')]))
    renderPage(<AwardsSection />)

    expect(await screen.findByRole('heading', { name: 'Awards' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Math olympiad gold' }).getAttribute('href')).toBe('/news/math-olympiad')
    expect(api.listPublicNews).toHaveBeenCalledWith({ Page: 1, PageSize: 3, Category: 'Award' })
  })

  it('shows nothing without awards', async () => {
    api.listPublicNews.mockResolvedValue(pageOf([]))
    const { container } = renderPage(<AwardsSection />)

    await waitFor(() => expect(api.listPublicNews).toHaveBeenCalled())
    expect(container.querySelector('section')).toBeNull()
  })

  it('shows nothing when the news cannot be loaded', async () => {
    api.listPublicNews.mockRejectedValueOnce(apiError(500))
    const { container } = renderPage(<AwardsSection />)

    await waitFor(() => expect(api.listPublicNews).toHaveBeenCalled())
    expect(container.querySelector('section')).toBeNull()
  })
})
