import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { usePendingRequests } from '../api/usePendingRequests'
import '../i18n'

/** The principal's card of requests that wait for a decision (F-09), with a link to the approvals. */
export function PendingRequestsCard() {
  const { t } = useTranslation('dashboard')
  const { data, isPending, isError, refetch } = usePendingRequests()

  return (
    <Card className="min-w-0 lg:col-span-2">
      <CardHeader>
        <CardTitle>{t('analytics.requests.title')}</CardTitle>
        <CardDescription>{t('analytics.requests.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-wrap items-center justify-between gap-4">
        {isPending ? (
          <div role="status" aria-busy="true">
            <Skeleton className="h-10 w-40" />
            <span className="sr-only">{t('analytics.requests.loading')}</span>
          </div>
        ) : isError ? (
          <ErrorNotice message={t('analytics.requests.error')} onRetry={() => void refetch()} />
        ) : (
          <>
            <p className="text-3xl font-semibold tabular-nums">
              {t('analytics.requests.count', { count: data })}
            </p>
            <Link to="/requests/inbox" className={buttonVariants({ variant: 'outline' })}>
              {t('analytics.requests.open')}
            </Link>
          </>
        )}
      </CardContent>
    </Card>
  )
}
