import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, pageOf, renderPage, signInAs } from '@/test/utils'
import type { AuditLog } from '../api/types'
import { AuditLogPage } from './AuditLogPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const calls = () => http.customInstance.mock.calls.filter(([c]) => c.url === '/api/v1/audit-logs').map(([c]) => c)

const log = (overrides: Partial<AuditLog> = {}): AuditLog => ({
  id: 'l1',
  at: '2026-11-04T03:00:00Z',
  userId: 'u1',
  userName: 'Budi Santoso',
  action: 'Updated',
  entityName: 'Grade',
  entityId: 'g1',
  oldValues: '{"Score":70}',
  newValues: '{"Score":85}',
  ...overrides,
})

beforeEach(() => {
  http.customInstance.mockReset()
  signInAs('Admin', 'M Ridhwan Habib A')
})

describe('AuditLogPage', () => {
  it('lists the recorded changes with who made them and the old and new values', async () => {
    http.customInstance.mockResolvedValue(pageOf([log(), log({ id: 'l2', action: 'Created', userId: null, userName: null, entityName: 'AttendanceRecord', oldValues: null, newValues: '{"Status":"Sick"}' })]))

    renderPage(<AuditLogPage />, '/manage/audit-log')

    const table = await screen.findByRole('table', { name: 'Recorded changes, newest first' })
    const changed = within(table).getByRole('row', { name: /Budi Santoso/ })
    expect(changed.textContent).toContain('Changed')
    expect(changed.textContent).toContain('Grade')
    expect(changed.textContent).toContain('Score: 70 → 85')
    const created = within(table).getByRole('row', { name: /System/ })
    expect(created.textContent).toContain('Created')
    expect(created.textContent).toContain('Attendance')
    expect(created.textContent).toContain('Status: Sick')
    expect(calls()[0]?.params).toEqual({ entityName: undefined, from: undefined, to: undefined, page: 1, pageSize: 20 })
  })

  it('shows the values of a deleted row and survives values that are not JSON', async () => {
    http.customInstance.mockResolvedValue(pageOf([log({ action: 'Deleted', oldValues: '{"Score":50}', newValues: null }), log({ id: 'l2', oldValues: 'oops', newValues: 'not json' })]))

    renderPage(<AuditLogPage />, '/manage/audit-log')

    const table = await screen.findByRole('table')
    expect(within(table).getAllByRole('row')[1]?.textContent).toContain('Score: 50')
    expect(within(table).getAllByRole('row')[2]?.textContent).toContain('No details')
  })

  it('asks for one kind of record and one period when the filters change, starting again at page 1', async () => {
    http.customInstance.mockResolvedValue(pageOf([log()]))
    renderPage(<AuditLogPage />, '/manage/audit-log')
    await screen.findByRole('table')

    fireEvent.change(screen.getByLabelText('Kind of record'), { target: { value: 'Approval' } })
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-11-01' } })
    fireEvent.change(screen.getByLabelText('Until'), { target: { value: '2026-11-04' } })

    await waitFor(() => expect(calls().at(-1)?.params).toEqual({ entityName: 'Approval', from: '2026-11-01', to: '2026-11-04', page: 1, pageSize: 20 }))
  })

  it('shows an empty state, and an error with a retry', async () => {
    http.customInstance.mockResolvedValueOnce(pageOf([]))
    const { unmount } = renderPage(<AuditLogPage />, '/manage/audit-log')
    expect(await screen.findByText('No changes recorded')).toBeTruthy()
    unmount()

    http.customInstance.mockRejectedValueOnce(apiError(500))
    renderPage(<AuditLogPage />, '/manage/audit-log')
    expect((await screen.findByRole('alert')).textContent).toContain('The audit log could not be loaded.')
  })
})
