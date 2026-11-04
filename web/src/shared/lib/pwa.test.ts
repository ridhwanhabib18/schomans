import { afterEach, describe, expect, it, vi } from 'vitest'
import { registerServiceWorker } from './pwa'

const browserWith = (register: () => Promise<unknown>) => ({ serviceWorker: { register } }) as unknown as Navigator

afterEach(() => {
  vi.restoreAllMocks()
})

describe('registerServiceWorker', () => {
  it('registers /sw.js once the page has loaded, in a production build', () => {
    const register = vi.fn().mockResolvedValue({})
    const listen = vi.spyOn(window, 'addEventListener')

    registerServiceWorker(true, browserWith(register))
    expect(register).not.toHaveBeenCalled()
    const onLoad = listen.mock.calls.find(([type]) => type === 'load')?.[1] as () => void
    onLoad()

    expect(register).toHaveBeenCalledWith('/sw.js')
  })

  it('does nothing in development, where a cached shell would hide every change', () => {
    const register = vi.fn()
    const listen = vi.spyOn(window, 'addEventListener')

    registerServiceWorker(false, browserWith(register))

    expect(listen).not.toHaveBeenCalledWith('load', expect.anything())
    expect(register).not.toHaveBeenCalled()
  })

  it('does nothing in a browser without service workers', () => {
    const listen = vi.spyOn(window, 'addEventListener')

    registerServiceWorker(true, {} as Navigator)

    expect(listen).not.toHaveBeenCalledWith('load', expect.anything())
  })

  it('ignores a failed registration', async () => {
    const register = vi.fn().mockRejectedValue(new Error('blocked'))
    const listen = vi.spyOn(window, 'addEventListener')

    registerServiceWorker(true, browserWith(register))
    const onLoad = listen.mock.calls.find(([type]) => type === 'load')?.[1] as () => void

    expect(() => onLoad()).not.toThrow()
    await Promise.resolve()
  })
})
