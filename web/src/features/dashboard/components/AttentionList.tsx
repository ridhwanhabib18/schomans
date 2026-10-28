import { useTranslation } from 'react-i18next'
import { Badge } from '@/shared/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { EmptyState } from '@/shared/ui/page-parts'
import type { AttentionStudent, TeacherAnalytics } from '../api/analyticsTypes'
import '../i18n'

/** Teacher: the students whose attendance or grades call for attention in the current term. */
export function AttentionList({ analytics }: { analytics: TeacherAnalytics }) {
  const { t } = useTranslation('dashboard')

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
      </CardHeader>
      <CardContent>
        <DataTable
          caption={t('analytics.attention.caption')}
          columns={columns}
          rows={analytics.flagged}
          rowKey={(s) => s.studentId}
          isLoading={false}
          empty={<EmptyState title={t('analytics.attention.empty')} />}
        />
      </CardContent>
    </Card>
  )
}
