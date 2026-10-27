import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { NewsCategory } from '@/api/generated/model'
import { useDocumentMeta } from '@/shared/hooks/useDocumentMeta'
import { cn } from '@/shared/lib/utils'
import { Pagination } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePublicNewsList } from '../api/news'
import { NewsCard } from '../components/NewsCard'
import '../i18n'

const PAGE_SIZE = 9
const CATEGORIES: readonly NewsCategory[] = ['News', 'AdmissionInfo', 'Award']

/** The public news page: published articles, newest first, with a category filter. Open to everybody. */
export function PublicNewsListPage() {
  const { t } = useTranslation('news')
  const [category, setCategory] = useState<NewsCategory | undefined>()
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = usePublicNewsList(page, PAGE_SIZE, category)

  useDocumentMeta({ title: t('public.title'), description: t('public.description') })

  function choose(next: NewsCategory | undefined) {
    setCategory(next)
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('public.title')} description={t('public.description')} />

      <div role="group" aria-label={t('public.filterLabel')} className="flex flex-wrap gap-2">
        {[undefined, ...CATEGORIES].map((value) => (
          <button
            key={value ?? 'all'}
            type="button"
            aria-pressed={category === value}
            onClick={() => choose(value)}
            className={cn(
              'rounded-full border px-3 py-1 text-sm',
              category === value ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-card hover:bg-muted',
            )}
          >
            {value ? t(`categories.${value}`) : t('public.all')}
          </button>
        ))}
      </div>

      {isPending ? (
        <div role="status" aria-busy="true" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-64" />
          ))}
        </div>
      ) : isError ? (
        <ErrorNotice message={t('public.error')} onRetry={() => void refetch()} />
      ) : data.items.length === 0 ? (
        <EmptyState title={t('public.empty')} />
      ) : (
        <>
          <ul aria-label={t('public.caption')} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {data.items.map((article) => (
              <li key={article.slug} className="relative">
                <NewsCard article={article} />
              </li>
            ))}
          </ul>
          <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />
        </>
      )}
    </div>
  )
}
