import { useQuery } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import type { Analytics } from './analyticsTypes'

export const analyticsKeys = {
  all: ['dashboard', 'analytics'] as const,
}

const getAnalytics = () => customInstance<Analytics>({ url: '/api/v1/dashboard/analytics', method: 'GET' })

/** The charts and lists of the dashboard for the signed-in role (F-09). Only the part of the role is filled. */
export function useAnalytics(enabled = true) {
  return useQuery({ queryKey: analyticsKeys.all, queryFn: () => getAnalytics(), enabled })
}
