import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import { openInNewTab } from '@/shared/lib/download'

export type GeneralLetterKind = 'Invitation' | 'Notice'

export interface GeneralLetter {
  id: string
  kind: GeneralLetterKind
  letterNumber: string
  recipient: string
  subject: string
  signedByName: string
  issuedAt: string
}

export interface IssueLetterRequest {
  kind: GeneralLetterKind
  recipient: string
  subject: string
  paragraphs: string[]
}

interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export const PAGE_SIZE = 20

export const letterKeys = {
  all: ['general-letters'] as const,
  list: (page: number) => [...letterKeys.all, 'list', page] as const,
}

/** The general letters issued so far, newest first. */
export function useGeneralLetters(page: number) {
  return useQuery({
    queryKey: letterKeys.list(page),
    queryFn: () => customInstance<Paged<GeneralLetter>>({ url: '/api/v1/general-letters', method: 'GET', params: { page, pageSize: PAGE_SIZE } }),
    placeholderData: keepPreviousData,
  })
}

/** Issues a letter: the API numbers it, adds the letterhead and the signature, and stores the PDF. */
export function useIssueGeneralLetter() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: IssueLetterRequest) => customInstance<GeneralLetter>({ url: '/api/v1/general-letters', method: 'POST', data: request }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: letterKeys.all }),
  })
}

/** Opens the PDF of a letter through a short-lived address. */
export function useOpenGeneralLetter() {
  return useMutation({
    mutationFn: async (id: string) => {
      const { url } = await customInstance<{ url: string }>({ url: `/api/v1/general-letters/${id}/url`, method: 'GET' })
      openInNewTab(url)
    },
  })
}
