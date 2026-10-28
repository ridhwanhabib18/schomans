import { ArrowLeft } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useDocumentMeta } from '@/shared/hooks/useDocumentMeta'
import { formatDate } from '@/shared/lib/format'
import { buttonVariants } from '@/shared/ui/button'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { RichTextContent } from '@/shared/ui/rich-text-content'
import { Skeleton } from '@/shared/ui/skeleton'
import { useMarkAnnouncementRead, useMyAnnouncement } from '../api/announcements'
import '../i18n'

/** One announcement. Opening it marks it read (and clears its notification), once. */
export function AnnouncementDetailPage() {
  const { t } = useTranslation('announcements')
  const { id = '' } = useParams()
  const { data, isPending, isError } = useMyAnnouncement(id)
  const markRead = useMarkAnnouncementRead()
  const marked = useRef<string | null>(null)

  useDocumentMeta({ title: data?.title ?? '' })

  useEffect(() => {
    if (data && !data.isRead && marked.current !== data.id) {
      marked.current = data.id
      markRead.mutate(data.id)
    }
  }, [data, markRead])

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Link to="/announcements" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('detail.back')}
      </Link>

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-9 w-2/3" />
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('detail.error')} />
      ) : (
        <article className="flex flex-col gap-4">
          {data.coverImageUrl && <img src={data.coverImageUrl} alt="" className="max-h-80 w-full rounded-xl object-cover" />}
          <header>
            <h1 className="text-2xl font-semibold tracking-tight">{data.title}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t('detail.published', { date: formatDate(data.publishedAt) })}</p>
          </header>
          <RichTextContent html={data.body} />
        </article>
      )}
    </div>
  )
}
