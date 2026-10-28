import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formatDate } from '@/shared/lib/format'
import { cn } from '@/shared/lib/utils'
import { Checkbox } from '@/shared/ui/form-controls'
import { Pagination } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useMyAnnouncements } from '../api/announcements'
import '../i18n'

const PAGE_SIZE = 10

/** The announcements meant for the signed-in user, newest first, with a mark on the unread ones. */
export function AnnouncementsInboxPage() {
  const { t } = useTranslation('announcements')
  const [page, setPage] = useState(1)
  const [unreadOnly, setUnreadOnly] = useState(false)
  const { data, isPending, isError, refetch } = useMyAnnouncements(page, PAGE_SIZE, unreadOnly)

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('inbox.title')} description={t('inbox.description')} />

      <label className="flex w-fit items-center gap-2 text-sm">
        <Checkbox
          checked={unreadOnly}
          onChange={(event) => {
            setUnreadOnly(event.target.checked)
            setPage(1)
          }}
        />
        {t('inbox.unreadOnly')}
      </label>

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('inbox.error')} onRetry={() => void refetch()} />
      ) : data.items.length === 0 ? (
        <EmptyState
          title={unreadOnly ? t('inbox.emptyUnread.title') : t('inbox.empty.title')}
          description={unreadOnly ? t('inbox.emptyUnread.description') : t('inbox.empty.description')}
        />
      ) : (
        <>
          <ul aria-label={t('inbox.caption')} className="flex flex-col gap-3">
            {data.items.map((announcement) => (
              <li key={announcement.id}>
                <Link
                  to={`/announcements/${announcement.id}`}
                  className={cn(
                    'flex gap-4 rounded-lg border border-border bg-card p-4 transition-colors hover:bg-muted/50',
                    !announcement.isRead && 'border-primary/40 bg-primary/5',
                  )}
                >
                  {announcement.coverImageUrl && (
                    <img src={announcement.coverImageUrl} alt="" className="hidden size-20 shrink-0 rounded-md object-cover sm:block" />
                  )}
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      {!announcement.isRead && <span aria-label={t('inbox.unread')} className="size-2 shrink-0 rounded-full bg-primary" />}
                      <h2 className={cn('truncate text-base', announcement.isRead ? 'font-medium' : 'font-semibold')}>{announcement.title}</h2>
                    </div>
                    <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{announcement.excerpt}</p>
                    <p className="mt-2 text-xs text-muted-foreground">{formatDate(announcement.publishedAt)}</p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
          <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />
        </>
      )}
    </div>
  )
}
