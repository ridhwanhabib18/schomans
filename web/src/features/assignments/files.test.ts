import { describe, expect, it } from 'vitest'
import { ATTACHMENT_TYPES, checkFile, formatSize, MAX_FILE_BYTES, SUBMISSION_TYPES } from './files'

describe('checkFile', () => {
  it('accepts a PDF and refuses an Office file for a student', () => {
    expect(checkFile({ type: 'application/pdf', size: 1000 }, SUBMISSION_TYPES)).toBeNull()
    expect(checkFile({ type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', size: 1000 }, SUBMISSION_TYPES)).toBe('type')
  })

  it('lets a teacher attach Office files too', () => {
    expect(checkFile({ type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation', size: 1000 }, ATTACHMENT_TYPES)).toBeNull()
    expect(checkFile({ type: 'application/zip', size: 1000 }, ATTACHMENT_TYPES)).toBe('type')
  })

  it('allows exactly 10 MB and refuses one byte more or an empty file', () => {
    expect(checkFile({ type: 'image/png', size: MAX_FILE_BYTES }, SUBMISSION_TYPES)).toBeNull()
    expect(checkFile({ type: 'image/png', size: MAX_FILE_BYTES + 1 }, SUBMISSION_TYPES)).toBe('size')
    expect(checkFile({ type: 'image/png', size: 0 }, SUBMISSION_TYPES)).toBe('size')
  })
})

describe('formatSize', () => {
  it('shows kilobytes and megabytes', () => {
    expect(formatSize(820 * 1024)).toBe('820 KB')
    expect(formatSize(1.5 * 1024 * 1024)).toBe('1.5 MB')
    expect(formatSize(10)).toBe('1 KB')
  })
})
