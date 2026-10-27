import { useMutation } from '@tanstack/react-query'
import { createUploadUrl } from '@/api/generated/schomans'
import { putToPresignedUrl, validateCoverImage } from '@/shared/lib/upload'

export type CoverModule = 'news' | 'announcements'

/** Why a picked file was refused before anything was sent: a wrong type or a wrong size. */
export class CoverImageError extends Error {
  readonly reason: 'type' | 'size'

  constructor(reason: 'type' | 'size') {
    super(`The cover image has the wrong ${reason}.`)
    this.name = 'CoverImageError'
    this.reason = reason
  }
}

/**
 * The browser's side of a cover image upload: ask the API for a presigned URL (it checks the role, the record, the type
 * and the size), then send the file straight to storage. Resolves with the key to save on the record.
 */
export function useCoverUpload(module: CoverModule, entityId: string | undefined) {
  return useMutation({
    mutationFn: async (file: File) => {
      if (!entityId) {
        throw new Error('Save the record before uploading its image.')
      }

      const refused = validateCoverImage(file)
      if (refused) {
        throw new CoverImageError(refused)
      }

      const issued = await createUploadUrl({ module, entityId, contentType: file.type, sizeBytes: file.size })
      await putToPresignedUrl(issued.url, file, issued.contentType)
      return issued.key
    },
  })
}
