import { useQuery } from '@tanstack/react-query'
import { listMyChildren } from '@/api/generated/schomans'

export const myChildrenKey = ['me', 'children'] as const

/** The children linked to the signed-in parent. Shared by the parent pages of several features. */
export function useMyChildren(enabled = true) {
  return useQuery({ queryKey: myChildrenKey, queryFn: () => listMyChildren(), enabled, staleTime: 5 * 60_000 })
}
