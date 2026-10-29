import { Lock } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useTermOptions } from '@/shared/hooks/useTermOptions'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useGenerateReportCards, useGradeBook, useGradingOptions, usePublishGrades, useUnpublishGrades } from '../api/grading'
import type { GradeBook, GradeBookRow, GradeComponent } from '../api/types'
import { ENTERABLE_COMPONENTS } from '../api/types'
import { DescriptionDialog } from '../components/DescriptionDialog'
import { ScoreCell } from '../components/ScoreCell'
import { WeightsDialog } from '../components/WeightsDialog'
import { gradingErrorText } from '../errors'
import '../i18n'

type Action = 'publish' | 'unpublish' | 'generate'

const averageOf = (row: GradeBookRow, component: GradeComponent) => row.components.find((c) => c.component === component)?.average ?? null

function ResultBadge({ row }: { row: GradeBookRow }) {
  const { t } = useTranslation('grading')

  if (row.isPassing === null) {
    return <span className="text-muted-foreground">{t('book.noGrade')}</span>
  }

  return <Badge variant={row.isPassing ? 'success' : 'destructive'}>{row.isPassing ? t('book.passing') : t('book.below')}</Badge>
}

function GradeBookView({ book }: { book: GradeBook }) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const publish = usePublishGrades()
  const unpublish = useUnpublishGrades()
  const generate = useGenerateReportCards()
  const [editing, setEditing] = useState<GradeBookRow | null>(null)
  const [weightsOpen, setWeightsOpen] = useState(false)
  const [action, setAction] = useState<Action | null>(null)
  const [actionError, setActionError] = useState<string>()

  const target = { classroomId: book.classroomId, termId: book.termId }
  const pending = publish.isPending || unpublish.isPending || generate.isPending

  function confirm() {
    if (!action) {
      return
    }

    setActionError(undefined)
    const fail = (error: unknown) => setActionError(gradingErrorText(error, t, tCommon('errors.generic')))
    const done = (title: string) => {
      setAction(null)
      showToast({ kind: 'success', title })
    }

    if (action === 'publish') {
      publish.mutate(target, { onSuccess: () => done(t('book.publish.done')), onError: fail })
    } else if (action === 'unpublish') {
      unpublish.mutate(target, { onSuccess: () => done(t('book.unpublish.done')), onError: fail })
    } else {
      generate.mutate(target, { onSuccess: (result) => done(t('book.generate.done', { count: result.count })), onError: fail })
    }
  }

  const columns: Column<GradeBookRow>[] = [
    { key: 'student', header: t('book.columns.student'), render: (r) => <span className="font-medium">{r.studentName}</span> },
    {
      key: 'Assignment',
      header: t('components.Assignment'),
      className: 'tabular-nums',
      render: (r) => averageOf(r, 'Assignment') ?? '—',
    },
    ...ENTERABLE_COMPONENTS.map((component) => ({
      key: component,
      header: t(`components.${component}`),
      render: (r: GradeBookRow) => (
        <ScoreCell
          studentId={r.studentId}
          studentName={r.studentName}
          subjectId={book.subjectId}
          termId={book.termId}
          component={component}
          value={averageOf(r, component)}
          locked={book.isPublished}
        />
      ),
    })),
    { key: 'final', header: t('book.columns.final'), className: 'font-semibold tabular-nums', render: (r) => r.finalGrade ?? '—' },
    { key: 'band', header: t('book.columns.band'), render: (r) => r.band ?? '—' },
    { key: 'status', header: t('book.columns.status'), render: (r) => <ResultBadge row={r} /> },
    {
      key: 'description',
      header: t('book.columns.description'),
      className: 'text-right',
      render: (r) => (
        <Button variant="outline" size="sm" onClick={() => setEditing(r)}>
          {t('book.actions.text')}
          <span className="sr-only">: {r.studentName}</span>
        </Button>
      ),
    },
  ]

  const confirmCopy = action ? t(`book.${action}.title`) : ''

  return (
    <section className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Badge variant={book.isPublished ? 'success' : 'muted'}>{book.isPublished ? t('book.published') : t('book.draft')}</Badge>
        <span className="text-sm text-muted-foreground">{t('book.minimum', { score: book.minimumPassingScore })}</span>
        <div className="ml-auto flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setWeightsOpen(true)}>
            {t('book.actions.weights')}
          </Button>
          {book.isPublished ? (
            <Button variant="outline" onClick={() => setAction('unpublish')}>
              {t('book.actions.unpublish')}
            </Button>
          ) : (
            <Button onClick={() => setAction('publish')}>{t('book.actions.publish')}</Button>
          )}
          <Button variant="secondary" disabled={!book.isPublished} onClick={() => setAction('generate')}>
            {t('book.actions.generate')}
          </Button>
        </div>
      </div>

      {book.isPublished && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Lock aria-hidden="true" className="size-4" />
          <span>{t('book.lockedNote')}</span>
        </div>
      )}

      <DataTable
        caption={t('book.caption')}
        columns={columns}
        rows={book.rows}
        rowKey={(r) => r.studentId}
        isLoading={false}
        empty={<EmptyState title={t('book.empty')} />}
      />

      {weightsOpen && <WeightsDialog subjectId={book.subjectId} onClose={() => setWeightsOpen(false)} />}
      {editing && (
        <DescriptionDialog
          studentId={editing.studentId}
          studentName={editing.studentName}
          subjectId={book.subjectId}
          termId={book.termId}
          initial={editing.description}
          locked={book.isPublished}
          onClose={() => setEditing(null)}
        />
      )}
      <ConfirmDialog
        open={action !== null}
        onOpenChange={(open) => {
          if (!open) {
            setAction(null)
            setActionError(undefined)
          }
        }}
        title={confirmCopy}
        description={
          <span className="flex flex-col gap-3">
            {action && <span>{t(`book.${action}.body`)}</span>}
            {actionError && (
              <span role="alert" className="font-medium text-destructive">
                {actionError}
              </span>
            )}
          </span>
        }
        confirmLabel={confirmCopy}
        pending={pending}
        onConfirm={confirm}
      />
    </section>
  )
}

/** The teacher's grade book: pick a class, a subject and a term, then enter scores, write report texts, and publish. */
export function GradeBookPage() {
  const { t } = useTranslation('grading')
  const options = useGradingOptions()
  const terms = useTermOptions()
  const [choice, setChoice] = useState('')
  const [chosenTerm, setChosenTerm] = useState<string>()

  const termId = chosenTerm ?? terms.data?.find((term) => term.isActive)?.id ?? ''
  const [classroomId = '', subjectId = ''] = choice.split('|')
  const params = classroomId && subjectId && termId ? { classroomId, subjectId, termId } : null
  const book = useGradeBook(params)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('book.title')} description={t('book.description')} />

      {options.isPending || terms.isPending ? (
        <Skeleton className="h-10 w-full max-w-xl" />
      ) : options.isError || terms.isError ? (
        <ErrorNotice message={t('book.error')} />
      ) : options.data.length === 0 ? (
        <EmptyState title={t('book.pick.none')} />
      ) : (
        <div className="flex flex-wrap gap-4">
          <label className="flex min-w-0 flex-col sm:min-w-64 gap-1.5 text-sm font-medium">
            {t('book.pick.classAndSubject')}
            <Select value={choice} onChange={(event) => setChoice(event.target.value)}>
              <option value="">{t('book.pick.choose')}</option>
              {options.data.map((option) => (
                <option key={`${option.classroomId}|${option.subjectId}`} value={`${option.classroomId}|${option.subjectId}`}>
                  {option.classroomName} - {option.subjectName}
                </option>
              ))}
            </Select>
          </label>
          <label className="flex min-w-48 flex-col gap-1.5 text-sm font-medium">
            {t('book.pick.term')}
            <Select value={termId} onChange={(event) => setChosenTerm(event.target.value)}>
              {terms.data.map((term) => (
                <option key={term.id} value={term.id}>
                  {term.label}
                </option>
              ))}
            </Select>
          </label>
        </div>
      )}

      {params === null ? (
        !options.isPending && options.data && options.data.length > 0 && <p className="text-sm text-muted-foreground">{t('book.pick.prompt')}</p>
      ) : book.isPending ? (
        <Skeleton className="h-64 w-full" />
      ) : book.isError ? (
        <ErrorNotice message={gradingErrorText(book.error, t, t('book.error'))} onRetry={() => void book.refetch()} />
      ) : (
        <GradeBookView key={`${params.classroomId}|${params.subjectId}|${params.termId}`} book={book.data} />
      )}
    </div>
  )
}
