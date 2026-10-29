import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { useDashboard } from '../api/useDashboard'
import { AnalyticsSection } from '../components/AnalyticsSection'
import { DashboardSkeleton } from '../components/DashboardSkeleton'
import { EmptyDashboard } from '../components/EmptyDashboard'
import '../i18n'

/** The roles that have charts or lists (F-09); the others keep the empty dashboard. */
const HAS_ANALYTICS = ['Principal', 'Teacher', 'Student', 'Parent']

export function DashboardPage() {
  const { t } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation()
  const { data, isPending, isError, refetch, isFetching } = useDashboard()

  if (isPending) {
    return <DashboardSkeleton />
  }

  if (isError) {
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p>{t('error')}</p>
        <Button variant="outline" disabled={isFetching} onClick={() => void refetch()}>
          {tCommon('actions.retry')}
        </Button>
      </div>
    )
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">{t(`title.${data.role}`)}</h1>
        <p className="text-muted-foreground">{t('welcome', { name: data.fullName })}</p>
      </header>
      {HAS_ANALYTICS.includes(data.role) ? <AnalyticsSection /> : data.sections.length === 0 && <EmptyDashboard roleLabel={tCommon(`roles.${data.role}`)} />}
    </div>
  )
}
