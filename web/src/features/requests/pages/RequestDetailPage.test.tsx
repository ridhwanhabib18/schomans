import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderRoutes, signInAs } from '@/test/utils'
import { detail } from '../testing'
import { RequestDetailPage } from './RequestDetailPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const open = vi.hoisted(() => ({ tab: vi.fn() }))
vi.mock('@/shared/lib/download', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/download')>()), openInNewTab: open.tab }))

const sent = (path: string) => http.customInstance.mock.calls.map(([c]) => c).filter((c) => c.url === path && c.method === 'POST')

/** The API as the page sees it: the request, and whatever the POSTs answer. */
function serve(request = detail(), posts: Record<string, unknown> = {}) {
  http.customInstance.mockImplementation(async ({ url, method }: { url: string; method: string }) => {
    if (method === 'GET' && url === '/api/v1/requests/r1') return request
    if (method === 'GET' && url === '/api/v1/letters/r1/url') return { url: 'https://storage.test/letter.pdf?sig=1', expiresAt: '2026-11-03T03:05:00Z', letterNumber: '001/SK/SCHOMANS/XI/2026' }
    if (method === 'POST' && url in posts) {
      const answer = posts[url]
      if (answer instanceof Error) throw answer
      return answer
    }
    throw new Error(`unexpected request ${method} ${url}`)
  })
}

const renderDetail = () => renderRoutes([{ path: '/requests/:id', element: <RequestDetailPage /> }], '/requests/r1')

beforeEach(() => {
  http.customInstance.mockReset()
  open.tab.mockReset()
  signInAs('Parent', 'Agus Putra')
})

describe('RequestDetailPage', () => {
  it('shows the request, the chain with the current step marked and the decisions so far', async () => {
    serve(
      detail({
        currentStep: 2,
        waitingFor: 'Principal',
        approvals: [{ step: 1, role: 'HomeroomTeacher', approverName: 'Budi Santoso', decision: 'Approved', notes: 'Get well soon', decidedAt: '2026-10-31T03:00:00Z' }],
      }),
    )
    renderDetail()

    expect(await screen.findByRole('heading', { level: 1, name: 'Student leave' })).toBeTruthy()
    expect(screen.getByText('Waiting')).toBeTruthy()
    expect(screen.getByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText('Class A')).toBeTruthy()
    expect(screen.getByText('02/11/2026 to 04/11/2026')).toBeTruthy()
    expect(screen.getByText('Family event')).toBeTruthy()

    const steps = screen.getAllByRole('listitem').filter((item) => item.textContent?.startsWith('Step '))
    expect(steps.map((s) => s.textContent)).toEqual(['Step 1: Homeroom teacherDecided', 'Step 2: PrincipalWaiting now'])
    expect(steps[1]?.getAttribute('aria-current')).toBe('step')

    expect(screen.getByText('Homeroom teacher: Budi Santoso')).toBeTruthy()
    expect(screen.getByText('Get well soon')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it('says nobody decided yet', async () => {
    serve()
    renderDetail()

    expect(await screen.findByText('Nobody has decided yet.')).toBeTruthy()
  })

  it('lets the approver whose turn it is approve, with an optional note', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(detail({ canDecide: true }), { '/api/v1/requests/r1/decision': detail({ currentStep: 2, waitingFor: 'Principal' }) })
    renderDetail()

    expect(await screen.findByText('This request waits for your decision.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Approve this request?' })
    fireEvent.change(within(dialog).getByLabelText('Note (optional)'), { target: { value: ' Fine ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(sent('/api/v1/requests/r1/decision')).toHaveLength(1))
    expect(sent('/api/v1/requests/r1/decision')[0]?.data).toEqual({ id: 'r1', approve: true, note: 'Fine' })
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
  })

  it('needs a note to reject, and then ends the request with it', async () => {
    signInAs('Principal', 'Hendra Wijaya')
    serve(detail({ canDecide: true }), { '/api/v1/requests/r1/decision': detail({ status: 'Rejected', waitingFor: null }) })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Reject this request?' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reject' }))

    expect(await within(dialog).findByText('Give the reason for the rejection.')).toBeTruthy()
    expect(sent('/api/v1/requests/r1/decision')).toHaveLength(0)

    fireEvent.change(within(dialog).getByLabelText('Note'), { target: { value: 'Not this week' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reject' }))

    await waitFor(() => expect(sent('/api/v1/requests/r1/decision')).toHaveLength(1))
    expect(sent('/api/v1/requests/r1/decision')[0]?.data).toEqual({ id: 'r1', approve: false, note: 'Not this week' })
  })

  it('shows the answer of the API when the turn has passed', async () => {
    signInAs('Teacher', 'Budi Santoso')
    serve(detail({ canDecide: true }), { '/api/v1/requests/r1/decision': apiError(403, { code: 'requests.not_your_turn' }) })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('alertdialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Approve' }))

    expect((await screen.findByRole('alert')).textContent).toContain('This request does not wait for your decision.')
  })

  it('lets the requester withdraw a request that still waits, after asking', async () => {
    serve(detail({ canCancel: true }), { '/api/v1/requests/r1/cancel': detail({ status: 'Cancelled', waitingFor: null }) })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Withdraw the request' }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Withdraw this request?' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Withdraw' }))

    await waitFor(() => expect(sent('/api/v1/requests/r1/cancel')).toHaveLength(1))
  })

  it('shows neither button to somebody who may only read the request', async () => {
    serve(detail({ canDecide: false, canCancel: false }))
    renderDetail()

    await screen.findByRole('heading', { level: 1 })
    expect(screen.queryByRole('button', { name: 'Withdraw the request' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Reject' })).toBeNull()
  })

  it('opens the letter of an approved certificate request through its short-lived address', async () => {
    serve(detail({ type: 'CertificateLetter', status: 'Approved', waitingFor: null, startDate: null, endDate: null, letter: { letterNumber: '001/SK/SCHOMANS/XI/2026', issuedAt: '2026-11-03T03:00:00Z' } }))
    renderDetail()

    expect(await screen.findByText('Letter number 001/SK/SCHOMANS/XI/2026')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Download letter (PDF)' }))

    await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/letter.pdf?sig=1'))
  })

  it('has no letter section before a letter exists', async () => {
    serve(detail({ type: 'CertificateLetter' }))
    renderDetail()

    await screen.findByRole('heading', { level: 1, name: 'Certificate letter' })
    expect(screen.queryByRole('button', { name: 'Download letter (PDF)' })).toBeNull()
  })

  it('says why a request cannot be opened', async () => {
    http.customInstance.mockRejectedValue(apiError(403, { code: 'requests.not_yours' }))
    renderDetail()

    expect((await screen.findByRole('alert')).textContent).toContain('You may not open this request.')
  })
})
