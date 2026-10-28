import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formatDate } from '@/shared/lib/format'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePublicAnnouncements } from '../api/announcements'
import '../i18n'

/** The public announcements for the landing page. Nothing to show, or a failure, simply renders nothing: the page still works. */
export function PublicAnnouncementsSection({ count = 5 }: { count?: number }) {
  const { t } = useTranslation('announcements')
  const { data, isPending, isError } = usePublicAnnouncements(1, count)

  if (isError || (!isPending && data.items.length === 0)) {
    return null
  }

  return (
    <section aria-labelledby="public-announcements" className="flex flex-col gap-4">
      <h2 id="public-announcements" className="text-xl font-semibold">
        {t('public.latest')}
      </h2>
      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-2">
          <Skeleton className="h-16" />
          <Skeleton className="h-16" />
        </div>
      ) : (
        <ul className="flex flex-col divide-y rounded-xl border bg-card">
          {data.items.map((announcement) => (
            <li key={announcement.id}>
              <Link to={`/announcements/public/${announcement.id}`} className="flex flex-col gap-1 p-4 hover:bg-muted/50">
                <span className="font-medium">{announcement.title}</span>
                <span className="text-sm text-muted-foreground">{announcement.excerpt}</span>
                <time dateTime={announcement.publishedAt} className="text-xs text-muted-foreground">
                  {formatDate(announcement.publishedAt)}
                </time>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
