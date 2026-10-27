import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePublicNewsList } from '../api/news'
import { NewsCard } from './NewsCard'
import '../i18n'

/** The newest published articles for the landing page. A failure or an empty site simply shows nothing: the page still works. */
export function LatestNews({ count = 3 }: { count?: number }) {
  const { t } = useTranslation('news')
  const { data, isPending } = usePublicNewsList(1, count)

  if (!isPending && (!data || data.items.length === 0)) {
    return null
  }

  return (
    <section aria-labelledby="latest-news" className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="latest-news" className="text-xl font-semibold">
          {t('public.latest')}
        </h2>
        <Link to="/news" className={buttonVariants({ variant: 'link', size: 'sm' })}>
          {t('public.viewAll')}
        </Link>
      </div>
      {isPending ? (
        <div role="status" aria-busy="true" className="grid gap-4 sm:grid-cols-3">
          <Skeleton className="h-56" />
          <Skeleton className="h-56" />
          <Skeleton className="h-56" />
        </div>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map((article) => (
            <li key={article.slug} className="relative">
              <NewsCard article={article} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
