import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderPage, signInAs } from '@/test/utils'
import { chains } from '../testing'
import type { ApprovalChain } from '../api/types'
import { ApprovalChainsPage } from './ApprovalChainsPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const puts = () => http.customInstance.mock.calls.map(([c]) => c).filter((c) => c.method === 'PUT')

function serve(list: ApprovalChain[] = chains, put?: unknown) {
  http.customInstance.mockImplementation(async ({ method }: { method: string }) => {
    if (method === 'PUT') {
      if (put instanceof Error) throw put
      return put ?? list
    }
    return list
  })
}

const list = (name: string) => screen.getByRole('list', { name })

beforeEach(() => {
  http.customInstance.mockReset()
})

describe('ApprovalChainsPage as the admin staff', () => {
  beforeEach(() => signInAs('Admin', 'M Ridhwan Habib A'))

  it('shows the chain of every kind of request as ordered steps', async () => {
    serve()
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')

    const leave = await screen.findByRole('list', { name: 'Steps of the chain of Student leave' })
    expect(within(leave).getByLabelText('Who decides at step 1')).toHaveProperty('value', 'HomeroomTeacher')
    expect(within(leave).getByLabelText('Who decides at step 2')).toHaveProperty('value', 'Principal')
    expect(within(list('Steps of the chain of Certificate letter')).getByLabelText('Who decides at step 1')).toHaveProperty('value', 'Admin')
  })

  it('adds a step, moves it up and saves the new order', async () => {
    serve()
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')
    const card = (await screen.findByRole('heading', { name: 'Certificate letter' })).closest('div[class*="rounded"]') as HTMLElement

    fireEvent.click(within(card).getByRole('button', { name: 'Add a step' }))
    fireEvent.click(within(card).getByRole('button', { name: 'Move step 3 up' }))
    fireEvent.change(within(card).getByLabelText('Who decides at step 2'), { target: { value: 'HomeroomTeacher' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Save the chain' }))

    await waitFor(() => expect(puts()).toHaveLength(1))
    expect(puts()[0]).toMatchObject({ url: '/api/v1/approval-chains', data: { type: 'CertificateLetter', roles: ['Admin', 'HomeroomTeacher', 'Principal'] } })
  })

  it('removes a step but keeps at least one', async () => {
    serve()
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')
    const steps = await screen.findByRole('list', { name: 'Steps of the chain of Teacher leave' })

    expect(within(steps).getByRole('button', { name: 'Remove step 1' })).toHaveProperty('disabled', true)
  })

  it('allows at most five steps', async () => {
    serve([{ type: 'StudentLeave', roles: ['HomeroomTeacher', 'Admin', 'Principal', 'Admin', 'Principal'], hasPendingRequests: false }])
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')

    expect(await screen.findByRole('button', { name: 'Add a step' })).toHaveProperty('disabled', true)
  })

  it('does not offer the homeroom teacher for a teacher\'s leave, which is not about a student', async () => {
    serve()
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')
    const steps = await screen.findByRole('list', { name: 'Steps of the chain of Teacher leave' })

    const option = within(steps).getByRole('option', { name: 'Homeroom teacher' }) as HTMLOptionElement
    expect(option.disabled).toBe(true)
  })

  it('does not let the chain of a kind with waiting requests change, and says why', async () => {
    serve([{ type: 'StudentLeave', roles: ['HomeroomTeacher', 'Principal'], hasPendingRequests: true }])
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')

    expect(await screen.findByText(/Requests of this kind are waiting for a decision, so the chain cannot change now\./)).toBeTruthy()
    expect(screen.getByLabelText('Who decides at step 1')).toHaveProperty('disabled', true)
    expect(screen.queryByRole('button', { name: 'Save the chain' })).toBeNull()
  })

  it('shows the conflict of the API when requests came in meanwhile', async () => {
    serve(chains, apiError(409, { code: 'requests.chain_in_use' }))
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')
    const card = (await screen.findByRole('heading', { name: 'Teacher leave' })).closest('div[class*="rounded"]') as HTMLElement

    fireEvent.click(within(card).getByRole('button', { name: 'Add a step' }))
    fireEvent.click(within(card).getByRole('button', { name: 'Save the chain' }))

    expect((await within(card).findByRole('alert')).textContent).toContain('Requests of this kind are waiting for a decision')
  })

  it('shows an error with a retry when the chains cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')

    expect((await screen.findByRole('alert')).textContent).toContain('The chains could not be loaded.')
  })
})

describe('ApprovalChainsPage as the principal', () => {
  it('can read the chains but not change them', async () => {
    signInAs('Principal', 'Hendra Wijaya')
    serve()
    renderPage(<ApprovalChainsPage />, '/manage/approval-chains')

    expect(await screen.findByText('You can read the chains; only the admin staff change them.')).toBeTruthy()
    const selects = await screen.findAllByLabelText(/Who decides at step 1/)
    expect(selects).toHaveLength(3)
    expect(selects.every((select) => (select as HTMLSelectElement).disabled)).toBe(true)
    expect(screen.queryByRole('button', { name: 'Save the chain' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Add a step' })).toBeNull()
  })
})
