import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDate } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { isNotPublished, useOpenReportCard, useStudentGrades, useStudentTerms, type GradeComponent, type SubjectGrade } from '../api/reportCards'
import '../i18n'

const COMPONENTS: readonly GradeComponent[] = ['Assignment', 'Quiz', 'Midterm', 'Final']

const averageOf = (subject: SubjectGrade, component: GradeComponent) => subject.components.find((c) => c.component === component)?.average ?? null

function GradesForTerm({ studentId, termId }: { studentId: string; termId: string }) {
  const { t } = useTranslation('reportcards')
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError, error, refetch } = useStudentGrades(studentId, termId)
  const open = useOpenReportCard()

  if (isPending) {
    return <Skeleton className="h-48 w-full" />
  }

  if (isError) {
    return isNotPublished(error) ? (
      <EmptyState title={t('notPublished.title')} description={t('notPublished.body')} />
    ) : (
      <ErrorNotice message={t('error')} onRetry={() => void refetch()} />
    )
  }

  const columns: Column<SubjectGrade>[] = [
    { key: 'subject', header: t('columns.subject'), render: (s) => <span className="font-medium">{s.subjectName}</span> },
    ...COMPONENTS.map((component) => ({
      key: component,
      header: t(`components.${component}`),
      className: 'tabular-nums',
      render: (s: SubjectGrade) => averageOf(s, component) ?? '—',
    })),
    { key: 'final', header: t('columns.final'), className: 'font-semibold tabular-nums', render: (s) => s.finalGrade ?? '—' },
    { key: 'band', header: t('columns.band'), render: (s) => s.band ?? '—' },
    {
      key: 'result',
      header: t('columns.result'),
      render: (s) =>
        s.isPassing === null ? (
          <span className="text-muted-foreground">{t('noGrade')}</span>
        ) : (
          <Badge variant={s.isPassing ? 'success' : 'destructive'}>{s.isPassing ? t('passing') : t('below')}</Badge>
        ),
    },
  ]

  const described = data.subjects.filter((s) => s.description)

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <span className="text-sm text-muted-foreground">{t('published', { date: formatDate(data.publishedAt) })}</span>
        {data.hasReportCard && (
          <Button
            className="ml-auto"
            disabled={open.isPending}
            onClick={() =>
              open.mutate(
                { studentId, termId },
                { onError: () => showToast({ kind: 'error', title: t('report.error') }) },
              )
            }
          >
            {t('report.download')}
          </Button>
        )}
      </div>

      <DataTable
        caption={t('caption')}
        columns={columns}
        rows={data.subjects}
        rowKey={(s) => s.subjectId}
        isLoading={false}
        empty={<EmptyState title={t('empty')} />}
      />

      {described.length > 0 && (
        <section aria-labelledby="achievements" className="flex flex-col gap-3">
          <h2 id="achievements" className="text-lg font-semibold">
            {t('achievement')}
          </h2>
          <dl className="flex flex-col gap-3">
            {described.map((s) => (
              <div key={s.subjectId} className="rounded-lg border border-border p-4">
                <dt className="font-medium">{s.subjectName}</dt>
                <dd className="mt-1 text-sm text-muted-foreground">{s.description}</dd>
              </div>
            ))}
          </dl>
        </section>
      )}
    </div>
  )
}

/** The published grades of one student with a term picker (the current term first). */
export function StudentGradesView({ studentId }: { studentId: string }) {
  const { t } = useTranslation('reportcards')
  const terms = useStudentTerms(studentId)
  const [chosen, setChosen] = useState<string>()

  if (terms.isPending) {
    return <Skeleton className="h-10 w-64" />
  }

  if (terms.isError) {
    return <ErrorNotice message={t('error')} onRetry={() => void terms.refetch()} />
  }

  const termId = chosen ?? terms.data.find((term) => term.isActive)?.id ?? terms.data[0]?.id ?? ''
  if (termId === '') {
    return <EmptyState title={t('noTerms')} />
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <label className="flex max-w-xs flex-col gap-1.5 text-sm font-medium">
        {t('term')}
        <Select value={termId} onChange={(event) => setChosen(event.target.value)}>
          {terms.data.map((term) => (
            <option key={term.id} value={term.id}>
              {term.academicYearName} - {term.name}
            </option>
          ))}
        </Select>
      </label>
      <GradesForTerm key={`${studentId}|${termId}`} studentId={studentId} termId={termId} />
    </div>
  )
}
