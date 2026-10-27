import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { PublicNewsSummaryDto } from '@/api/generated/model'
import { formatDate } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/badge'
import '../i18n'

/** One article in a public list: cover, category, title, excerpt, date. The whole card is the link. */
export function NewsCard({ article }: { article: PublicNewsSummaryDto }) {
  const { t } = useTranslation('news')

  return (
    <article className="group flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-shadow hover:shadow-md">
      {article.coverImageUrl && <img src={article.coverImageUrl} alt="" loading="lazy" className="h-44 w-full object-cover" />}
      <div className="flex flex-1 flex-col gap-2 p-4">
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <Badge variant="muted">{t(`categories.${article.category}`)}</Badge>
          <time dateTime={article.publishedAt}>{formatDate(article.publishedAt)}</time>
        </div>
        <h3 className="text-lg font-semibold leading-snug">
          <Link to={`/news/${article.slug}`} className="after:absolute after:inset-0 hover:underline">
            {article.title}
          </Link>
        </h3>
        <p className="line-clamp-3 text-sm text-muted-foreground">{article.excerpt}</p>
      </div>
    </article>
  )
}
