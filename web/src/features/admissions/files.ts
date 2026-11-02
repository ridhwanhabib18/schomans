/** The files an application accepts, checked in the browser before anything is uploaded (the API checks again). */
export const MAX_DOCUMENT_BYTES = 10 * 1024 * 1024

export const DOCUMENT_TYPES = ['application/pdf', 'image/jpeg', 'image/png', 'image/webp'] as const

export type FileProblem = 'type' | 'size'

export function checkDocument(file: { type: string; size: number }): FileProblem | null {
  if (!(DOCUMENT_TYPES as readonly string[]).includes(file.type)) {
    return 'type'
  }

  return file.size === 0 || file.size > MAX_DOCUMENT_BYTES ? 'size' : null
}
