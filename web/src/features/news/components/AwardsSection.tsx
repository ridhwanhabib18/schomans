import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { usePublicNewsList } from '../api/news'
import { NewsCard } from './NewsCard'
import '../i18n'

/** The newest published articles of the category Awards, for the landing page. Without any, or on a failure, nothing shows. */
export function AwardsSection({ count = 3 }: { count?: number }) {
  const { t } = useTranslation('news')
  const { data } = usePublicNewsList(1, count, 'Award')

  if (!data || data.items.length === 0) {
    return null
  }

  return (
    <section aria-labelledby="awards" className="flex min-w-0 flex-col gap-4">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="awards" className="text-xl font-semibold">
          {t('public.awards.title')}
        </h2>
        <Link to="/news" className={buttonVariants({ variant: 'link', size: 'sm' })}>
          {t('public.awards.viewAll')}
        </Link>
      </div>
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {data.items.map((article) => (
          <li key={article.slug} className="relative">
            <NewsCard article={article} />
          </li>
        ))}
      </ul>
    </section>
  )
}
