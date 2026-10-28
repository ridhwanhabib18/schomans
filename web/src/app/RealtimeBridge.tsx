import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { announcementKeys } from '@/features/announcements/api/announcements'
import { assignmentKeys } from '@/features/assignments/api/assignments'
import { notificationKeys } from '@/features/notifications/api/notifications'
import { createNotificationConnection, NOTIFICATION_CREATED, type NotificationPush } from '@/shared/lib/realtime'
import { selectAuthStatus, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'

const RETRY_DELAY_MS = 5000

/**
 * Keeps one SignalR connection open while somebody is signed in. A pushed notification refreshes the notification, announcement
 * and assignment queries (the badge, the lists) and shows a toast; after a dropped connection comes back, the same
 * refresh catches up on whatever was missed. It renders nothing.
 */
export function RealtimeBridge() {
  const status = useAuthStore(selectAuthStatus)
  const queryClient = useQueryClient()
  const showToast = useToastStore(selectShowToast)

  useEffect(() => {
    if (status !== 'authenticated') {
      return
    }

    const connection = createNotificationConnection(() => useAuthStore.getState().accessToken)
    let stopped = false
    let retry: ReturnType<typeof setTimeout> | undefined

    const refresh = () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
      void queryClient.invalidateQueries({ queryKey: announcementKeys.all })
      // A new assignment, a reminder or a score changes what the assignment pages show.
      void queryClient.invalidateQueries({ queryKey: assignmentKeys.all })
    }

    connection.on(NOTIFICATION_CREATED, (push: NotificationPush) => {
      refresh()
      showToast({ kind: 'info', title: push.title, body: push.body, linkUrl: push.linkUrl })
    })
    connection.onreconnected(refresh)

    // The first connect is not retried by SignalR itself, so retry it here until it works or the user signs out.
    const start = () => {
      connection.start().catch(() => {
        if (!stopped) {
          retry = setTimeout(start, RETRY_DELAY_MS)
        }
      })
    }
    start()

    return () => {
      stopped = true
      clearTimeout(retry)
      void connection.stop()
    }
  }, [status, queryClient, showToast])

  return null
}
