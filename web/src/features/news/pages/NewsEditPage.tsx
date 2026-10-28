import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useManagedNews } from '../api/news'
import { NewsForm } from '../components/NewsForm'
import '../i18n'

/** `/manage/news/new` writes a new article; `/manage/news/:id` edits a saved one. */
export function NewsEditPage() {
  const { t } = useTranslation('news')
  const { id } = useParams()
  const { data, isPending, isError, refetch } = useManagedNews(id)
  const loading = id !== undefined && isPending

  return (
    <div className="flex flex-col gap-6">
      <Link to="/manage/news" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('manage.title')}
      </Link>
      <PageHeader title={id ? t('form.editTitle') : t('form.newTitle')} />

      {loading ? (
        <div role="status" aria-busy="true" className="flex max-w-3xl flex-col gap-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('form.loadError')} onRetry={() => void refetch()} />
      ) : (
        <NewsForm key={data ? `${data.id}-${data.rowVersion}` : 'new'} article={data} onReload={() => void refetch()} />
      )}
    </div>
  )
}
