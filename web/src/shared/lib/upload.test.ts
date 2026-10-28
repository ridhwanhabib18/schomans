import { afterEach, describe, expect, it, vi } from 'vitest'
import { COVER_IMAGE_MAX_BYTES, putToPresignedUrl, validateCoverImage } from './upload'

describe('validateCoverImage', () => {
  it.each(['image/jpeg', 'image/png', 'image/webp'])('accepts %s', (type) => {
    expect(validateCoverImage({ type, size: 1024 })).toBeNull()
  })

  it.each(['image/gif', 'application/pdf', 'text/html', ''])('refuses the type "%s"', (type) => {
    expect(validateCoverImage({ type, size: 1024 })).toBe('type')
  })

  it('accepts a file of exactly the maximum size and refuses one byte more', () => {
    expect(validateCoverImage({ type: 'image/png', size: COVER_IMAGE_MAX_BYTES })).toBeNull()
    expect(validateCoverImage({ type: 'image/png', size: COVER_IMAGE_MAX_BYTES + 1 })).toBe('size')
  })

  it('refuses an empty file', () => {
    expect(validateCoverImage({ type: 'image/png', size: 0 })).toBe('size')
  })
})

describe('putToPresignedUrl', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sends the file with exactly the signed content type and no credentials header', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200 })
    vi.stubGlobal('fetch', fetchMock)
    const file = new Blob(['x'], { type: 'image/png' })

    await putToPresignedUrl('http://storage/upload?sig=1', file, 'image/png')

    expect(fetchMock).toHaveBeenCalledWith('http://storage/upload?sig=1', { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: file })
  })

  it('throws when storage rejects the file', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))

    await expect(putToPresignedUrl('http://storage/upload', new Blob(['x']), 'image/png')).rejects.toThrow('403')
  })
})
