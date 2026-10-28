import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import '@/shared/lib/i18n'
import { QrPanel } from './QrPanel'

const toDataURL = vi.hoisted(() => vi.fn())
vi.mock('qrcode', () => ({ default: { toDataURL } }))

const TOKEN = '0123456789abcdef0123456789abcdef.AbC123'
const qr = (nextRotationAt: string) => ({ token: TOKEN, rotatedAt: '2026-10-28T00:00:00Z', nextRotationAt })

afterEach(() => {
  vi.useRealTimers()
  toDataURL.mockReset()
})

describe('QrPanel', () => {
  it('draws the check-in address as the code', async () => {
    toDataURL.mockResolvedValue('data:image/png;base64,AAAA')
    render(<QrPanel qr={qr('2099-01-01T00:00:00Z')} />)

    const image = await screen.findByAltText('QR code to check in to this lesson')

    expect(image.getAttribute('src')).toBe('data:image/png;base64,AAAA')
    expect(toDataURL).toHaveBeenCalledWith(expect.stringContaining(`/attend?t=${encodeURIComponent(TOKEN)}`), expect.objectContaining({ errorCorrectionLevel: 'M' }))
  })

  it('counts down to the next code and says when it is any moment', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
    vi.setSystemTime(new Date('2026-10-28T00:00:00Z'))
    toDataURL.mockResolvedValue('data:image/png;base64,AAAA')
    render(<QrPanel qr={qr('2026-10-28T00:00:03Z')} />)

    expect(screen.getByText('New code in 3 s')).toBeTruthy()
    act(() => {
      vi.advanceTimersByTime(3000)
    })

    expect(screen.getByText('New code any moment')).toBeTruthy()
  })

  it('draws the new code when it is replaced', async () => {
    toDataURL.mockResolvedValue('data:image/png;base64,AAAA')
    const { rerender } = render(<QrPanel qr={qr('2099-01-01T00:00:00Z')} />)
    await screen.findByAltText('QR code to check in to this lesson')

    rerender(<QrPanel qr={{ ...qr('2099-01-01T00:01:00Z'), token: 'fedcba9876543210fedcba9876543210.Zzz' }} />)

    await waitFor(() => expect(toDataURL).toHaveBeenCalledTimes(2))
  })

  it('shows the hint to students in the normal size only', async () => {
    toDataURL.mockResolvedValue('data:image/png;base64,AAAA')
    const { rerender } = render(<QrPanel qr={qr('2099-01-01T00:00:00Z')} />)
    expect(screen.getByText(/Students scan this code/)).toBeTruthy()

    rerender(<QrPanel qr={qr('2099-01-01T00:00:00Z')} large />)

    expect(screen.queryByText(/Students scan this code/)).toBeNull()
  })
})
