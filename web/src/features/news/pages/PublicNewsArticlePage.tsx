import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useDocumentMeta } from '@/shared/hooks/useDocumentMeta'
import { formatDate } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/badge'
import { buttonVariants } from '@/shared/ui/button'
import { EmptyState } from '@/shared/ui/page-parts'
import { RichTextContent } from '@/shared/ui/rich-text-content'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePublicNews } from '../api/news'
import '../i18n'

/** One public article. Its title and description go into the page's meta tags, so links and search results show them. */
export function PublicNewsArticlePage() {
  const { t } = useTranslation('news')
  const { slug = '' } = useParams()
  const { data, isPending, isError } = usePublicNews(slug)

  useDocumentMeta({ title: data?.title ?? '', description: data?.metaDescription, image: data?.coverImageUrl })

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Link to="/news" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('public.back')}
      </Link>

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-10 w-3/4" />
          <Skeleton className="h-4 w-48" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : isError ? (
        <EmptyState title={t('public.notFound.title')} description={t('public.notFound.body')} />
      ) : (
        <article className="flex flex-col gap-5">
          {data.coverImageUrl && <img src={data.coverImageUrl} alt="" className="max-h-96 w-full rounded-xl object-cover" />}
          <header className="flex flex-col gap-2">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Badge variant="muted">{t(`categories.${data.category}`)}</Badge>
              <time dateTime={data.publishedAt}>{t('public.publishedOn', { date: formatDate(data.publishedAt) })}</time>
            </div>
            <h1 className="text-3xl font-bold tracking-tight">{data.title}</h1>
          </header>
          <RichTextContent html={data.body} />
        </article>
      )}
    </div>
  )
}
