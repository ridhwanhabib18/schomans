import { fireEvent, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useChildStore } from '@/stores/useChildStore'
import { apiError, renderRoutes, signInAs } from '@/test/utils'
import { detail } from '../testing'
import { NewRequestPage } from './NewRequestPage'

const api = vi.hoisted(() => ({ getMyAttendance: vi.fn(), listMyChildren: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const posted = () => http.customInstance.mock.calls.map(([c]) => c).filter((c) => c.method === 'POST')

function renderForm() {
  return renderRoutes(
    [
      { path: '/requests/new', element: <NewRequestPage /> },
      { path: '/requests/:id', element: <p>Request page</p> },
    ],
    '/requests/new',
  )
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  useChildStore.setState({ childId: null })
  api.getMyAttendance.mockResolvedValue({ studentId: 's1' })
  api.listMyChildren.mockResolvedValue([{ studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' }])
})

describe('NewRequestPage as a parent', () => {
  beforeEach(() => signInAs('Parent', 'Agus Putra'))

  it('sends a student leave for the chosen child with the days and the reason, then opens the request', async () => {
    http.customInstance.mockResolvedValue(detail({ id: 'new-1' }))
    renderForm()

    fireEvent.change(await screen.findByLabelText(/First day/), { target: { value: '2026-11-02' } })
    fireEvent.change(screen.getByLabelText(/Last day/), { target: { value: '2026-11-04' } })
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: '  Family event ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    expect(await screen.findByText('Request page')).toBeTruthy()
    expect(posted()).toEqual([
      { url: '/api/v1/requests', method: 'POST', data: { type: 'StudentLeave', studentId: 's1', startDate: '2026-11-02', endDate: '2026-11-04', reason: 'Family event' } },
    ])
  })

  it('asks for a certificate letter without days', async () => {
    http.customInstance.mockResolvedValue(detail({ id: 'new-2', type: 'CertificateLetter' }))
    renderForm()

    fireEvent.change(await screen.findByLabelText(/What do you ask for/), { target: { value: 'CertificateLetter' } })
    expect(screen.queryByLabelText(/First day/)).toBeNull()
    fireEvent.change(screen.getByLabelText(/What is the letter for/), { target: { value: 'A scholarship' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    await screen.findByText('Request page')
    expect(posted()[0]?.data).toEqual({ type: 'CertificateLetter', studentId: 's1', startDate: null, endDate: null, reason: 'A scholarship' })
  })

  it('does not send a request without days or reason, or with the last day before the first', async () => {
    renderForm()
    fireEvent.click(await screen.findByRole('button', { name: 'Send the request' }))

    expect(await screen.findByText('Give the first and the last day.')).toBeTruthy()
    expect(screen.getByText('Say what the request is for.')).toBeTruthy()

    fireEvent.change(screen.getByLabelText(/First day/), { target: { value: '2026-11-05' } })
    fireEvent.change(screen.getByLabelText(/Last day/), { target: { value: '2026-11-03' } })
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'Trip' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    expect(await screen.findByText('The last day is not before the first day.')).toBeTruthy()
    expect(posted()).toEqual([])
  })

  it('shows the answer of the API when it refuses the request', async () => {
    http.customInstance.mockRejectedValue(apiError(400, { code: 'requests.too_early' }))
    renderForm()

    fireEvent.change(await screen.findByLabelText(/First day/), { target: { value: '2026-09-01' } })
    fireEvent.change(screen.getByLabelText(/Last day/), { target: { value: '2026-09-02' } })
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'Late note' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    expect((await screen.findByRole('alert')).textContent).toContain('A leave cannot start more than a week in the past.')
    expect(screen.queryByText('Request page')).toBeNull()
  })

  it('shows the first validation message of the API when there is no business code', async () => {
    http.customInstance.mockRejectedValue(apiError(400, { title: 'One or more validation errors occurred.', errors: { Reason: ['The reason is too long.'] } }))
    renderForm()

    fireEvent.change(await screen.findByLabelText(/First day/), { target: { value: '2026-11-02' } })
    fireEvent.change(screen.getByLabelText(/Last day/), { target: { value: '2026-11-02' } })
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'x' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    expect((await screen.findByRole('alert')).textContent).toContain('The reason is too long.')
  })
})

describe('NewRequestPage as a student', () => {
  it('asks for a letter about themselves with the own student record', async () => {
    signInAs('Student', 'Rina Putri')
    http.customInstance.mockResolvedValue(detail({ id: 'new-3', type: 'CertificateLetter' }))
    renderForm()

    expect(screen.queryByLabelText(/What do you ask for/)).toBeNull()
    fireEvent.change(await screen.findByLabelText(/What is the letter for/), { target: { value: 'A bank account' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    await screen.findByText('Request page')
    expect(posted()[0]?.data).toEqual({ type: 'CertificateLetter', studentId: 's1', startDate: null, endDate: null, reason: 'A bank account' })
  })

  it('says so when the own student record cannot be found', async () => {
    signInAs('Student', 'Rina Putri')
    api.getMyAttendance.mockRejectedValue(apiError(500))
    renderForm()

    expect((await screen.findByRole('alert')).textContent).toContain('Your student record could not be found.')
  })
})

describe('NewRequestPage as a teacher', () => {
  it('asks for leave for themselves, about no student', async () => {
    signInAs('Teacher', 'Budi Santoso')
    http.customInstance.mockResolvedValue(detail({ id: 'new-4', type: 'TeacherLeave', studentId: null }))
    renderForm()

    fireEvent.change(await screen.findByLabelText(/First day/), { target: { value: '2026-11-10' } })
    fireEvent.change(screen.getByLabelText(/Last day/), { target: { value: '2026-11-12' } })
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'Training' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send the request' }))

    await waitFor(() => expect(posted()).toHaveLength(1))
    expect(posted()[0]?.data).toEqual({ type: 'TeacherLeave', studentId: null, startDate: '2026-11-10', endDate: '2026-11-12', reason: 'Training' })
    expect(api.listMyChildren).not.toHaveBeenCalled()
  })
})
