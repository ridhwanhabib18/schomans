import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { EmptyState } from '@/shared/ui/page-parts'
import type { AttentionStudent, TeacherAnalytics } from '../api/analyticsTypes'
import '../i18n'

/** How many students are shown before "Show all": a whole school can have a hundred, and the page would be miles long. */
export const ATTENTION_PREVIEW = 10

const reasonCount = (student: AttentionStudent) => (student.lowAttendance ? 1 : 0) + student.belowPassingSubjects.length

/** The most urgent first: more reasons, then the lowest attendance (no records last), then by name so the order is stable. */
export function byUrgency(a: AttentionStudent, b: AttentionStudent): number {
  return (
    reasonCount(b) - reasonCount(a) ||
    (a.attendanceRate ?? Number.POSITIVE_INFINITY) - (b.attendanceRate ?? Number.POSITIVE_INFINITY) ||
    a.studentName.localeCompare(b.studentName)
  )
}

/** Teacher: the students whose attendance or grades call for attention in the current term. */
export function AttentionList({ analytics }: { analytics: TeacherAnalytics }) {
  const { t } = useTranslation('dashboard')
  const [showAll, setShowAll] = useState(false)

  const sorted = [...analytics.flagged].sort(byUrgency)
  const total = sorted.length
  const capped = total > ATTENTION_PREVIEW
  const rows = showAll ? sorted : sorted.slice(0, ATTENTION_PREVIEW)

  const columns: Column<AttentionStudent>[] = [
    { key: 'student', header: t('analytics.attention.student'), render: (s) => <span className="font-medium">{s.studentName}</span> },
    { key: 'classroom', header: t('analytics.attention.classroom'), render: (s) => s.classroomName },
    {
      key: 'attendance',
      header: t('analytics.attention.attendance'),
      className: 'tabular-nums',
      render: (s) => (s.attendanceRate === null ? t('analytics.attention.noRecords') : `${s.attendanceRate}%`),
    },
    {
      key: 'reasons',
      header: t('analytics.attention.reasons'),
      render: (s) => (
        <span className="flex flex-wrap gap-1.5">
          {s.lowAttendance && <Badge variant="warning">{t('analytics.attention.lowAttendance')}</Badge>}
          {s.belowPassingSubjects.length > 0 && (
            <Badge variant="destructive">{t('analytics.attention.below', { subjects: s.belowPassingSubjects.join(', ') })}</Badge>
          )}
        </span>
      ),
    },
  ]

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('analytics.attention.title')}</CardTitle>
        <CardDescription>{t('analytics.attention.description', { threshold: analytics.attendanceThreshold })}</CardDescription>
        {total > 0 && (
          <p className="text-sm font-medium" data-testid="attention-total">
            {t('analytics.attention.total', { count: total })}
            {capped && !showAll && <span className="font-normal text-muted-foreground"> · {t('analytics.attention.shown', { shown: rows.length, count: total })}</span>}
          </p>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <DataTable
          caption={t('analytics.attention.caption')}
          columns={columns}
          rows={rows}
          rowKey={(s) => s.studentId}
          isLoading={false}
          empty={<EmptyState title={t('analytics.attention.empty')} />}
        />
        {capped && (
          <div>
            <Button variant="outline" aria-expanded={showAll} onClick={() => setShowAll((open) => !open)}>
              {showAll ? t('analytics.attention.showFewer') : t('analytics.attention.showAll', { count: total })}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
