import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getUnreadNotificationCount, listMyNotifications, markAllNotificationsRead, markNotificationRead } from '@/api/generated/schomans'

/** One factory for the query keys; the real-time bridge invalidates these when a notification arrives. */
export const notificationKeys = {
  all: ['notifications'] as const,
  list: (pageSize: number) => [...notificationKeys.all, 'list', { pageSize }] as const,
  unreadCount: () => [...notificationKeys.all, 'unread-count'] as const,
}

export function useUnreadNotificationCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => getUnreadNotificationCount(),
    select: (data) => data.count,
  })
}

export function useNotifications(pageSize = 10, enabled = true) {
  return useQuery({
    queryKey: notificationKeys.list(pageSize),
    queryFn: () => listMyNotifications({ Page: 1, PageSize: pageSize }),
    enabled,
  })
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => markAllNotificationsRead(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}
