/** The files assignments accept, checked in the browser before anything is uploaded (the API checks again). */
export const MAX_FILE_BYTES = 10 * 1024 * 1024

export const ATTACHMENT_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/webp',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
] as const

export const SUBMISSION_TYPES = ['application/pdf', 'image/jpeg', 'image/png', 'image/webp'] as const

export const MAX_ATTACHMENTS = 5

export type FileProblem = 'type' | 'size'

export function checkFile(file: { type: string; size: number }, accepted: readonly string[]): FileProblem | null {
  if (!accepted.includes(file.type)) {
    return 'type'
  }

  return file.size === 0 || file.size > MAX_FILE_BYTES ? 'size' : null
}

/** "1.5 MB", "820 KB". */
export function formatSize(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}
