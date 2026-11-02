import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiError, renderRoutes } from '@/test/utils'
import { VerifyLetterPage } from './VerifyLetterPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const renderVerify = (code = 'abc123def456') => renderRoutes([{ path: '/verify/:code', element: <VerifyLetterPage /> }], `/verify/${code}`)

beforeEach(() => {
  http.customInstance.mockReset()
})

describe('VerifyLetterPage', () => {
  it('shows that the letter is real with its number, student and date, for a visitor who is not signed in', async () => {
    http.customInstance.mockResolvedValue({ valid: true, letterNumber: '001/SK/SCHOMANS/XI/2026', studentName: 'Rina Putri', issuedOn: '2026-11-03' })
    renderVerify()

    expect(await screen.findByText('This letter is real')).toBeTruthy()
    expect(screen.getByText('001/SK/SCHOMANS/XI/2026')).toBeTruthy()
    expect(screen.getByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText('03/11/2026')).toBeTruthy()
    expect(http.customInstance).toHaveBeenCalledWith({ url: '/api/v1/letters/verify/abc123def456', method: 'GET' })
  })

  it('says no letter has this code, without an error', async () => {
    http.customInstance.mockResolvedValue({ valid: false, letterNumber: null, studentName: null, issuedOn: null })
    renderVerify('nonsense')

    expect(await screen.findByText('No letter with this code')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.queryByText('This letter is real')).toBeNull()
  })

  it('shows an error with a retry when the check fails', async () => {
    http.customInstance.mockRejectedValue(apiError(429))
    renderVerify()

    expect((await screen.findByRole('alert')).textContent).toContain('The letter could not be checked. Try again.')
  })

  it('says it is checking while it waits', () => {
    http.customInstance.mockReturnValue(new Promise(() => {}))
    renderVerify()

    expect(screen.getByRole('status').textContent).toContain('Checking the letter…')
  })
})
