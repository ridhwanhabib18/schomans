import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/api/generated/schomans'

/** One factory per feature for query keys; realtime events will call invalidateQueries with these. */
export const dashboardKeys = {
  all: ['dashboard'] as const,
  current: () => [...dashboardKeys.all, 'current'] as const,
}

/** The role-specific dashboard of the signed-in user. */
export function useDashboard() {
  return useQuery({
    queryKey: dashboardKeys.current(),
    queryFn: () => getDashboard(),
  })
}
