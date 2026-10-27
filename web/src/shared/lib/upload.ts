/**
 * Step 2 of an upload: the browser sends the file straight to storage with the presigned URL the API issued.
 * The request carries no Authorization header (the URL itself is the permission) and must repeat exactly the
 * Content-Type the URL was signed for.
 */
export async function putToPresignedUrl(url: string, file: Blob, contentType: string): Promise<void> {
  const response = await fetch(url, { method: 'PUT', headers: { 'Content-Type': contentType }, body: file })
  if (!response.ok) {
    throw new Error(`The upload was rejected by storage (${response.status}).`)
  }
}

export const COVER_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const
export const COVER_IMAGE_MAX_BYTES = 5 * 1024 * 1024

/** Checks a picked file against the rules the API enforces, so the user gets an answer before anything is sent. */
export function validateCoverImage(file: { type: string; size: number }): 'type' | 'size' | null {
  if (!(COVER_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return 'type'
  }

  return file.size > COVER_IMAGE_MAX_BYTES || file.size === 0 ? 'size' : null
}
