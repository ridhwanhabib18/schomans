import { useTranslation } from 'react-i18next'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useAnalytics } from '../api/useAnalytics'
import '../i18n'
import { AttendanceTrendChart } from './AttendanceTrendChart'
import { AttentionList } from './AttentionList'
import { ChildProgressChart } from './ChildProgressChart'
import { SubjectAverageChart } from './SubjectAverageChart'

/** The role-specific part of the dashboard (F-09): principal charts, the attention list of a teacher, the progress of a child. */
export function AnalyticsSection() {
  const { t } = useTranslation('dashboard')
  const { data, isPending, isError, refetch } = useAnalytics()

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="grid gap-6 lg:grid-cols-2 [&>*]:min-w-0">
        <Skeleton className="h-80" />
        <Skeleton className="h-80" />
        <span className="sr-only">{t('analytics.loading')}</span>
      </div>
    )
  }

  if (isError) {
    return <ErrorNotice message={t('analytics.error')} onRetry={() => void refetch()} />
  }

  return (
    <div className="grid gap-6 lg:grid-cols-2 [&>*]:min-w-0">
      {data.principal && (
        <>
          <AttendanceTrendChart trend={data.principal.attendanceTrend} />
          <SubjectAverageChart averages={data.principal.subjectAverages} />
        </>
      )}
      {data.teacher && (
        <div className="min-w-0 lg:col-span-2">
          <AttentionList analytics={data.teacher} />
        </div>
      )}
      {data.children.map((child) => (
        <ChildProgressChart key={child.studentId} child={child} />
      ))}
    </div>
  )
}
