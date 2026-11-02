import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { openInNewTab } from '@/shared/lib/download'
import { customInstance } from '@/shared/lib/http'
import type {
  ApprovalChain,
  ApproverRole,
  CreateRequestCommand,
  LetterUrl,
  LetterVerification,
  Paged,
  RequestDetail,
  RequestListItem,
  RequestStatus,
  RequestType,
} from './types'

export const requestKeys = {
  all: ['requests'] as const,
  mine: (status: RequestStatus | undefined, page: number) => [...requestKeys.all, 'mine', status ?? 'all', page] as const,
  inbox: (page: number) => [...requestKeys.all, 'inbox', page] as const,
  list: (status: RequestStatus | undefined, type: RequestType | undefined, page: number) => [...requestKeys.all, 'list', status ?? 'all', type ?? 'all', page] as const,
  detail: (id: string) => [...requestKeys.all, 'detail', id] as const,
  chains: () => [...requestKeys.all, 'chains'] as const,
  verify: (code: string) => ['letters', 'verify', code] as const,
}

export const PAGE_SIZE = 20

// ---- requests (the Phase 5 endpoints) ----

const listMine = (status: RequestStatus | undefined, page: number) =>
  customInstance<Paged<RequestListItem>>({ url: '/api/v1/requests/mine', method: 'GET', params: { status, page, pageSize: PAGE_SIZE } })

const listInbox = (page: number) => customInstance<Paged<RequestListItem>>({ url: '/api/v1/requests/inbox', method: 'GET', params: { page, pageSize: PAGE_SIZE } })

const listAll = (status: RequestStatus | undefined, type: RequestType | undefined, page: number) =>
  customInstance<Paged<RequestListItem>>({ url: '/api/v1/requests', method: 'GET', params: { status, type, page, pageSize: PAGE_SIZE } })

const getRequest = (id: string) => customInstance<RequestDetail>({ url: `/api/v1/requests/${id}`, method: 'GET' })

const createRequest = (command: CreateRequestCommand) => customInstance<RequestDetail>({ url: '/api/v1/requests', method: 'POST', data: command })

const decideRequest = (id: string, approve: boolean, note: string | null) =>
  customInstance<RequestDetail>({ url: `/api/v1/requests/${id}/decision`, method: 'POST', data: { id, approve, note } })

const cancelRequest = (id: string) => customInstance<RequestDetail>({ url: `/api/v1/requests/${id}/cancel`, method: 'POST' })

const getChains = () => customInstance<ApprovalChain[]>({ url: '/api/v1/approval-chains', method: 'GET' })

const setChain = (type: RequestType, roles: ApproverRole[]) => customInstance<ApprovalChain[]>({ url: '/api/v1/approval-chains', method: 'PUT', data: { type, roles } })

const getLetterUrl = (requestId: string) => customInstance<LetterUrl>({ url: `/api/v1/letters/${requestId}/url`, method: 'GET' })

const verifyLetter = (code: string) => customInstance<LetterVerification>({ url: `/api/v1/letters/verify/${encodeURIComponent(code)}`, method: 'GET' })

// ---- hooks ----

/** A decision, a new request or a withdrawal moves the lists, the inbox and the detail, so one coarse invalidation keeps them all right. */
function useInvalidateRequests() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: requestKeys.all })
}

export function useMyRequests(status: RequestStatus | undefined, page: number) {
  return useQuery({ queryKey: requestKeys.mine(status, page), queryFn: () => listMine(status, page) })
}

export function useRequestInbox(page: number) {
  return useQuery({ queryKey: requestKeys.inbox(page), queryFn: () => listInbox(page) })
}

export function useAllRequests(status: RequestStatus | undefined, type: RequestType | undefined, page: number) {
  return useQuery({ queryKey: requestKeys.list(status, type, page), queryFn: () => listAll(status, type, page) })
}

export function useRequest(id: string) {
  return useQuery({ queryKey: requestKeys.detail(id), queryFn: () => getRequest(id), enabled: id !== '' })
}

export function useCreateRequest() {
  const invalidate = useInvalidateRequests()
  return useMutation({ mutationFn: createRequest, onSuccess: invalidate })
}

export function useDecideRequest(id: string) {
  const invalidate = useInvalidateRequests()
  return useMutation({
    mutationFn: ({ approve, note }: { approve: boolean; note: string | null }) => decideRequest(id, approve, note),
    onSuccess: invalidate,
  })
}

export function useCancelRequest(id: string) {
  const invalidate = useInvalidateRequests()
  return useMutation({ mutationFn: () => cancelRequest(id), onSuccess: invalidate })
}

/** Opens the PDF of the letter through a short-lived address. */
export function useOpenLetter() {
  return useMutation({
    mutationFn: async (requestId: string) => {
      const { url } = await getLetterUrl(requestId)
      openInNewTab(url)
    },
  })
}

export function useApprovalChains() {
  return useQuery({ queryKey: requestKeys.chains(), queryFn: () => getChains() })
}

export function useSetApprovalChain() {
  const invalidate = useInvalidateRequests()
  return useMutation({ mutationFn: ({ type, roles }: { type: RequestType; roles: ApproverRole[] }) => setChain(type, roles), onSuccess: invalidate })
}

/** The check behind the QR code of a letter. An unknown code is an answer (`valid: false`), not an error. */
export function useVerifyLetter(code: string) {
  return useQuery({ queryKey: requestKeys.verify(code), queryFn: () => verifyLetter(code), enabled: code !== '', retry: false })
}
