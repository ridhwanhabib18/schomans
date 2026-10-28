import { useTranslation } from 'react-i18next'
import type { AttendanceCountsDto, StudentAttendanceDto } from '@/api/generated/model'
import { formatDate } from '@/shared/lib/format'
import { formatTime } from '@/shared/lib/time'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice } from '@/shared/ui/page-parts'
import { StatusBadge } from './StatusBadge'
import '../i18n'

type Entry = StudentAttendanceDto['entries'][number]

export function CountsSummary({ counts }: { counts: AttendanceCountsDto }) {
  const { t } = useTranslation('attendance')
  const items = [
    ['present', counts.present],
    ['excused', counts.excused],
    ['sick', counts.sick],
    ['absent', counts.absent],
    ['lessons', counts.total],
  ] as const

  return (
    <dl className="grid grid-cols-2 gap-3 sm:grid-cols-5">
      {items.map(([key, value]) => (
        <div key={key} className="rounded-lg border border-border bg-card p-3">
          <dt className="text-xs text-muted-foreground">{t(`counts.${key}`)}</dt>
          <dd className="text-2xl font-semibold tabular-nums">{value}</dd>
        </div>
      ))}
    </dl>
  )
}

/** One student's month: the counts, then every lesson with its status. Used by the student and the parent pages. */
export function StudentAttendanceView({
  data,
  isPending,
  isError,
  onRetry,
}: {
  data: StudentAttendanceDto | undefined
  isPending: boolean
  isError: boolean
  onRetry: () => void
}) {
  const { t } = useTranslation('attendance')

  if (isError) {
    return <ErrorNotice message={t('student.error')} onRetry={onRetry} />
  }

  const columns: Column<Entry>[] = [
    { key: 'date', header: t('student.columns.date'), render: (e) => formatDate(e.date) },
    { key: 'time', header: t('student.columns.time'), render: (e) => formatTime(e.startTime) },
    { key: 'lesson', header: t('student.columns.lesson'), render: (e) => e.subjectName },
    { key: 'status', header: t('student.columns.status'), render: (e) => <StatusBadge status={e.status} /> },
    { key: 'note', header: t('student.columns.note'), render: (e) => e.note ?? '' },
  ]

  return (
    <div className="flex flex-col gap-4">
      {data && (
        <>
          <CountsSummary counts={data.counts} />
          {data.counts.total > 0 && <p className="text-sm text-muted-foreground">{t('student.rate', { percent: data.counts.presentPercent })}</p>}
        </>
      )}
      <DataTable
        caption={t('student.caption')}
        columns={columns}
        rows={data?.entries}
        rowKey={(e) => e.sessionId}
        isLoading={isPending}
        empty={<EmptyState title={t('student.empty')} />}
      />
    </div>
  )
}
