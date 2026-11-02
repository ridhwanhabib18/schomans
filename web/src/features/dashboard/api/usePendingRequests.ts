import { useQuery } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'

export const pendingRequestsKeys = {
  all: ['dashboard', 'pending-requests'] as const,
}

interface InboxPage {
  totalCount: number
}

/** How many requests wait for the decision of the signed-in approver: one row is asked for, only the total is used. */
const getPendingRequests = async () => {
  const page = await customInstance<InboxPage>({ url: '/api/v1/requests/inbox', method: 'GET', params: { page: 1, pageSize: 1 } })
  return page.totalCount
}

export function usePendingRequests(enabled = true) {
  return useQuery({ queryKey: pendingRequestsKeys.all, queryFn: () => getPendingRequests(), enabled })
}
