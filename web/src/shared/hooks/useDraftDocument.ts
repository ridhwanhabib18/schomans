import { useMutation } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import type { DocumentDraft, DraftDocumentType } from '@/shared/lib/drafts'

export interface DraftRequest {
  type: DraftDocumentType
  audience: string
  notes: string
}

/** Asks the AI to draft a document from the kind, the audience and short notes. It uses one of the person's daily AI calls and stores nothing. */
export function useDraftDocument() {
  return useMutation({
    mutationFn: (request: DraftRequest) => customInstance<DocumentDraft>({ url: '/api/v1/drafts', method: 'POST', data: request }),
  })
}
