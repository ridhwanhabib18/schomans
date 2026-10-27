import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useDocumentMeta } from '@/shared/hooks/useDocumentMeta'
import { formatDate } from '@/shared/lib/format'
import { buttonVariants } from '@/shared/ui/button'
import { EmptyState } from '@/shared/ui/page-parts'
import { RichTextContent } from '@/shared/ui/rich-text-content'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePublicAnnouncement } from '../api/announcements'
import '../i18n'

/** One public announcement, open to guests. Only announcements marked public are served by the API. */
export function PublicAnnouncementPage() {
  const { t } = useTranslation('announcements')
  const { id = '' } = useParams()
  const { data, isPending, isError } = usePublicAnnouncement(id)

  useDocumentMeta({ title: data?.title ?? '', image: data?.coverImageUrl })

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Link to="/" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('public.back')}
      </Link>

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-9 w-2/3" />
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : isError ? (
        <EmptyState title={t('public.notFound.title')} description={t('public.notFound.body')} />
      ) : (
        <article className="flex flex-col gap-4">
          {data.coverImageUrl && <img src={data.coverImageUrl} alt="" className="max-h-80 w-full rounded-xl object-cover" />}
          <header>
            <h1 className="text-2xl font-semibold tracking-tight">{data.title}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t('public.published', { date: formatDate(data.publishedAt) })}</p>
          </header>
          <RichTextContent html={data.body} />
        </article>
      )}
    </div>
  )
}
