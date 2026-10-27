import { Bell } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { formatDateTime } from '@/shared/lib/format'
import { cn } from '@/shared/lib/utils'
import { Button } from '@/shared/ui/button'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications, useUnreadNotificationCount } from '../api/notifications'
import '../i18n'

/** The bell in the header: an unread badge, and a panel with the latest notifications. New ones arrive live (see RealtimeBridge). */
export function NotificationBell() {
  const { t } = useTranslation('notifications')
  const [open, setOpen] = useState(false)
  const panelId = useId()
  const container = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const unread = useUnreadNotificationCount()
  const list = useNotifications(10, open)
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllNotificationsRead()
  const count = unread.data ?? 0

  // Close on Escape and on a click outside, like a menu.
  useEffect(() => {
    if (!open) {
      return
    }

    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setOpen(false)
    const onClick = (event: MouseEvent) => {
      if (container.current && !container.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('mousedown', onClick)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('mousedown', onClick)
    }
  }, [open])

  return (
    <div ref={container} className="relative">
      <Button
        variant="ghost"
        size="icon"
        aria-label={count > 0 ? t('bell.labelUnread', { count }) : t('bell.label')}
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => setOpen((value) => !value)}
      >
        <Bell aria-hidden="true" />
        {count > 0 && (
          <span
            data-testid="unread-badge"
            className="absolute right-1 top-1 flex min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-semibold leading-4 text-destructive-foreground"
          >
            {count > 99 ? '99+' : count}
          </span>
        )}
      </Button>

      {open && (
        <div
          id={panelId}
          role="region"
          aria-label={t('bell.title')}
          className="absolute right-0 z-50 mt-2 w-[min(22rem,calc(100vw-2rem))] rounded-lg border border-border bg-card text-card-foreground shadow-lg"
        >
          <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
            <h2 className="text-sm font-semibold">{t('bell.title')}</h2>
            <Button variant="link" size="sm" className="h-auto p-0" disabled={count === 0 || markAll.isPending} onClick={() => markAll.mutate()}>
              {t('bell.markAll')}
            </Button>
          </div>

          <div className="max-h-96 overflow-y-auto">
            {list.isPending ? (
              <div role="status" aria-busy="true" className="flex flex-col gap-2 p-4">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
              </div>
            ) : list.isError ? (
              <div className="p-4">
                <ErrorNotice message={t('error')} onRetry={() => void list.refetch()} />
              </div>
            ) : list.data.items.length === 0 ? (
              <p className="p-6 text-center text-sm text-muted-foreground">{t('bell.empty')}</p>
            ) : (
              <ul className="divide-y divide-border">
                {list.data.items.map((notification) => (
                  <li key={notification.id}>
                    <button
                      type="button"
                      className={cn('flex w-full flex-col gap-0.5 px-4 py-3 text-left hover:bg-muted', !notification.isRead && 'bg-primary/5')}
                      onClick={() => {
                        if (!notification.isRead) {
                          markRead.mutate(notification.id)
                        }
                        setOpen(false)
                        void navigate(notification.linkUrl)
                      }}
                    >
                      <span className="flex items-center gap-2">
                        {!notification.isRead && <span aria-label={t('bell.unread')} className="size-2 shrink-0 rounded-full bg-primary" />}
                        <span className="text-sm font-medium">{notification.title}</span>
                      </span>
                      <span className="line-clamp-2 text-sm text-muted-foreground">{notification.body}</span>
                      <span className="text-xs text-muted-foreground">{formatDateTime(notification.createdAt)}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
