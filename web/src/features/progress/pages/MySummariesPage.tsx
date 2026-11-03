import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { formatDate } from '@/shared/lib/format'
import { formatMonth } from '@/shared/lib/time'
import { Card, CardContent } from '@/shared/ui/card'
import { ChildPicker } from '@/shared/ui/child-picker'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectSetChildId, useChildStore } from '@/stores/useChildStore'
import { useMySummaries } from '../api/progress'
import { SummaryBody } from '../components/SummaryBody'
import '../i18n'

function ChildSummaries({ studentId }: { studentId: string }) {
  const { t } = useTranslation('progress')
  const summaries = useMySummaries(studentId)

  if (summaries.isPending) {
    return <Skeleton className="h-40 w-full" />
  }

  if (summaries.isError) {
    return <ErrorNotice message={t('mine.error')} onRetry={() => void summaries.refetch()} />
  }

  if (summaries.data.items.length === 0) {
    return <EmptyState title={t('mine.empty')} />
  }

  return (
    <div className="flex flex-col gap-4">
      {summaries.data.items.map((summary) => (
        <Card key={summary.id}>
          <CardContent className="flex flex-col gap-3 pt-6">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h2 className="text-lg font-semibold">{formatMonth(summary.month)}</h2>
              {summary.sentAt && <span className="text-xs text-muted-foreground">{t('mine.sentOn', { date: formatDate(summary.sentAt) })}</span>}
            </div>
            <SummaryBody body={summary.body} />
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

/** A parent reads the monthly summaries the homeroom teacher sent about a child, newest first. */
export function ChildSummariesPage() {
  const { t } = useTranslation('progress')
  const [params] = useSearchParams()
  const setChildId = useChildStore(selectSetChildId)
  const linkedChild = params.get('studentId')

  // A notification links to the child it is about; the picker keeps the choice only if the child is really the parent's.
  useEffect(() => {
    if (linkedChild) {
      setChildId(linkedChild)
    }
  }, [linkedChild, setChildId])

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('mine.title')} description={t('mine.description')} />
      <ChildPicker>{(childId) => <ChildSummaries studentId={childId} />}</ChildPicker>
    </div>
  )
}
