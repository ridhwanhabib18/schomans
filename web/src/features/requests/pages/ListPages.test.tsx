import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import { listItem } from '../testing'
import { AllRequestsPage, MyRequestsPage, RequestInboxPage } from './ListPages'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const calls = (url: string) => http.customInstance.mock.calls.filter(([c]) => c.url === url).map(([c]) => c)

beforeEach(() => {
  http.customInstance.mockReset()
})

describe('MyRequestsPage', () => {
  beforeEach(() => signInAs('Parent', 'Agus Putra'))

  it('lists the requests with their kind, student, days and where they wait, and links to the request', async () => {
    http.customInstance.mockResolvedValue(
      pageOf([listItem(), listItem({ id: 'r2', type: 'CertificateLetter', startDate: null, endDate: null, status: 'Approved', waitingFor: null, currentStep: 2 })]),
    )
    renderPage(<MyRequestsPage />, '/requests')

    const table = await screen.findByRole('table', { name: 'Your requests' })
    const leave = within(table).getByRole('row', { name: /Student leave/ })
    expect(within(leave).getByRole('link', { name: 'Student leave' }).getAttribute('href')).toBe('/requests/r1')
    expect(leave.textContent).toContain('Rina Putri')
    expect(leave.textContent).toContain('02/11/2026 to 04/11/2026')
    expect(leave.textContent).toContain('Waiting')
    expect(leave.textContent).toContain('Step 1 of 2: Homeroom teacher')
    const letter = within(table).getByRole('row', { name: /Certificate letter/ })
    expect(letter.textContent).toContain('Approved')
    expect(letter.textContent).not.toContain('Step ')
    expect(screen.getByRole('link', { name: 'New request' }).getAttribute('href')).toBe('/requests/new')
    expect(calls('/api/v1/requests/mine')[0]?.params).toEqual({ status: undefined, page: 1, pageSize: 20 })
  })

  it('asks for one status when the filter is changed', async () => {
    http.customInstance.mockResolvedValue(pageOf([listItem()]))
    renderPage(<MyRequestsPage />, '/requests')
    await screen.findByRole('table')

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Rejected' } })

    await waitFor(() => expect(calls('/api/v1/requests/mine').at(-1)?.params).toEqual({ status: 'Rejected', page: 1, pageSize: 20 }))
  })

  it('shows an empty state, and an error with a retry', async () => {
    http.customInstance.mockResolvedValueOnce(pageOf([]))
    const { unmount } = renderPage(<MyRequestsPage />, '/requests')
    expect(await screen.findByText('No requests yet')).toBeTruthy()
    unmount()

    http.customInstance.mockRejectedValueOnce(apiError(500))
    renderPage(<MyRequestsPage />, '/requests')
    expect((await screen.findByRole('alert')).textContent).toContain('The requests could not be loaded.')
  })
})

describe('RequestInboxPage', () => {
  it('lists the requests that wait for the approver, with who asked', async () => {
    signInAs('Teacher', 'Budi Santoso')
    http.customInstance.mockResolvedValue(pageOf([listItem(), listItem({ id: 'r3', requesterName: 'Siti Aminah', studentName: 'Oka Setiawan' })]))
    renderPage(<RequestInboxPage />, '/requests/inbox')

    const table = await screen.findByRole('table', { name: 'Requests that wait for your decision' })
    expect(within(table).getAllByRole('row')).toHaveLength(3)
    expect(table.textContent).toContain('Siti Aminah')
    expect(calls('/api/v1/requests/inbox')[0]?.params).toEqual({ page: 1, pageSize: 20 })
  })

  it('says when nothing waits', async () => {
    signInAs('Principal', 'Hendra Wijaya')
    http.customInstance.mockResolvedValue(pageOf([]))
    renderPage(<RequestInboxPage />, '/requests/inbox')

    expect(await screen.findByText('Nothing waits for you')).toBeTruthy()
  })
})

describe('AllRequestsPage', () => {
  beforeEach(() => signInAs('Admin', 'Sari Utami'))

  it('filters by status and by kind and goes back to the first page', async () => {
    http.customInstance.mockResolvedValue(pageOf([listItem()], { totalCount: 45 }))
    renderPage(<AllRequestsPage />, '/manage/requests')
    await screen.findByRole('table', { name: 'All requests' })

    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'TeacherLeave' } })
    await waitFor(() => expect(calls('/api/v1/requests').at(-1)?.params).toEqual({ status: undefined, type: 'TeacherLeave', page: 1, pageSize: 20 }))

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Pending' } })
    await waitFor(() => expect(calls('/api/v1/requests').at(-1)?.params).toEqual({ status: 'Pending', type: 'TeacherLeave', page: 1, pageSize: 20 }))
  })

  it('shows an empty state when no request matches', async () => {
    http.customInstance.mockResolvedValue(pageOf([]))
    renderPage(<AllRequestsPage />, '/manage/requests')

    expect(await screen.findByText('No requests match')).toBeTruthy()
  })
})
