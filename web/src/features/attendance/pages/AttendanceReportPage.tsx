import { Download } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StudentAttendanceRowDto } from '@/api/generated/model'
import { useActiveClassroomOptions } from '@/shared/hooks/useActiveClassroomOptions'
import { parseProblem } from '@/shared/lib/problem'
import { currentMonth } from '@/shared/lib/time'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { Select } from '@/shared/ui/form-controls'
import { MonthPicker } from '@/shared/ui/month-picker'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useClassroomSummary, useExportSummary } from '../api/attendance'
import '../i18n'

/** PRD F-03: attendance summaries per student, classroom and month, exportable to Excel. */
export function AttendanceReportPage() {
  const { t } = useTranslation('attendance')
  const showToast = useToastStore(selectShowToast)
  const classrooms = useActiveClassroomOptions()
  const [chosen, setChosen] = useState('')
  const [month, setMonth] = useState(currentMonth)
  const classroomId = chosen || classrooms.data?.[0]?.id || ''
  const classroomName = classrooms.data?.find((c) => c.id === classroomId)?.name ?? ''
  const { data, isPending, isError, error, refetch } = useClassroomSummary(classroomId, month)
  const exportSummary = useExportSummary()

  const columns: Column<StudentAttendanceRowDto>[] = [
    { key: 'name', header: t('report.columns.name'), render: (s) => <span className="font-medium">{s.fullName}</span> },
    { key: 'nisn', header: t('report.columns.nisn'), render: (s) => <span className="font-mono">{s.nisn}</span> },
    { key: 'present', header: t('report.columns.present'), className: 'text-right tabular-nums', render: (s) => s.counts.present },
    { key: 'excused', header: t('report.columns.excused'), className: 'text-right tabular-nums', render: (s) => s.counts.excused },
    { key: 'sick', header: t('report.columns.sick'), className: 'text-right tabular-nums', render: (s) => s.counts.sick },
    { key: 'absent', header: t('report.columns.absent'), className: 'text-right tabular-nums', render: (s) => s.counts.absent },
    {
      key: 'rate',
      header: t('report.columns.rate'),
      className: 'text-right tabular-nums',
      render: (s) =>
        s.counts.total === 0 ? (
          '—'
        ) : (
          <span className="inline-flex items-center gap-2">
            {s.counts.presentPercent}%{s.counts.presentPercent < 80 && <Badge variant="warning">{t('report.lowAttendance')}</Badge>}
          </span>
        ),
    },
  ]

  const forbidden = isError && parseProblem(error).status === 403

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('report.title')}
        description={t('report.description')}
        actions={
          <Button
            variant="outline"
            disabled={!data || exportSummary.isPending}
            onClick={() =>
              exportSummary.mutate(
                { classroomId, classroomName, month },
                { onError: () => showToast({ kind: 'error', title: t('report.exportFailed') }) },
              )
            }
          >
            <Download aria-hidden="true" />
            {exportSummary.isPending ? t('report.exporting') : t('report.export')}
          </Button>
        }
      />

      <div className="flex flex-wrap items-end gap-4">
        <label className="flex min-w-48 flex-col gap-1.5 text-sm font-medium">
          {t('report.classroom')}
          <Select value={classroomId} onChange={(event) => setChosen(event.target.value)}>
            {classrooms.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </label>
        <MonthPicker month={month} onChange={setMonth} />
      </div>

      {forbidden ? (
        <ErrorNotice message={t('errors.attendance.not_your_classroom')} />
      ) : isError ? (
        <ErrorNotice message={t('report.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          {data && <p className="text-sm text-muted-foreground">{t('report.lessons', { count: data.sessionCount })}</p>}
          <DataTable
            caption={t('report.caption')}
            columns={columns}
            rows={data?.students}
            rowKey={(s) => s.studentId}
            isLoading={classroomId !== '' && isPending}
            empty={<EmptyState title={t('report.empty')} />}
          />
        </>
      )}
    </div>
  )
}
