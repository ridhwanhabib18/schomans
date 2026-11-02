import { screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { PublicAdmissionPeriod } from '../api/admissions'
import { AdmissionsBanner } from './AdmissionsBanner'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const period = (overrides: Partial<PublicAdmissionPeriod> = {}): PublicAdmissionPeriod => ({
  id: 'p1',
  name: 'Admissions 2027/2028',
  opensAt: '2026-10-01T00:00:00Z',
  closesAt: '2026-12-01T00:00:00Z',
  resultsAnnouncedAt: '2026-12-15T03:00:00Z',
  reregistrationDeadline: '2027-01-05T03:00:00Z',
  isOpen: true,
  ...overrides,
})

const serve = (answer: PublicAdmissionPeriod | null) => http.customInstance.mockResolvedValue({ period: answer })

beforeEach(() => {
  http.customInstance.mockReset()
  useAuthStore.getState().clearSession()
})

describe('AdmissionsBanner', () => {
  it('announces that applications are open until a date and lets a guest apply', async () => {
    serve(period())
    renderPage(<AdmissionsBanner />)

    expect(await screen.findByText('Applications are open until 01/12/2026 07:00 WIB.')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Admissions 2027/2028' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Apply now' }).getAttribute('href')).toBe('/admissions')
    expect(http.customInstance.mock.calls[0]?.[0]).toMatchObject({ url: '/api/v1/admissions/open', method: 'GET' })
  })

  it('says when applications will open, without a button', async () => {
    serve(period({ isOpen: false, opensAt: '2027-01-10T00:00:00Z', closesAt: '2027-03-01T00:00:00Z' }))
    renderPage(<AdmissionsBanner />)

    expect(await screen.findByText('Applications open on 10/01/2027 07:00 WIB.')).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Apply now' })).toBeNull()
  })

  it('shows nothing when the school has no period to announce', async () => {
    serve(null)
    const { container } = renderPage(<AdmissionsBanner />)

    await waitFor(() => expect(http.customInstance).toHaveBeenCalled())
    expect(container.querySelector('section')).toBeNull()
  })

  it('shows nothing when the answer fails: the landing page still works', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    const { container } = renderPage(<AdmissionsBanner />)

    await waitFor(() => expect(http.customInstance).toHaveBeenCalled())
    expect(container.querySelector('section')).toBeNull()
  })

  it('offers the button to an applicant, but not to a teacher who has nothing to apply for', async () => {
    serve(period())
    signInAs('Applicant')
    const applicant = renderPage(<AdmissionsBanner />)
    expect(await screen.findByRole('link', { name: 'Apply now' })).toBeTruthy()
    applicant.unmount()

    signInAs('Teacher')
    renderPage(<AdmissionsBanner />)
    expect(await screen.findByText(/Applications are open until/)).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Apply now' })).toBeNull()
  })
})
