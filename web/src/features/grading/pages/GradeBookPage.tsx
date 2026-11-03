import { Lock, Sparkles } from 'lucide-react'
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
import {
  useDraftClassroomDescriptions,
  useGenerateReportCards,
  useGradeBook,
  useGradingOptions,
  useLearningObjectives,
  usePublishGrades,
  useUnpublishGrades,
} from '../api/grading'
import type { GradeBook, GradeBookRow, GradeComponent } from '../api/types'
import { ENTERABLE_COMPONENTS } from '../api/types'
import { DescriptionDialog } from '../components/DescriptionDialog'
import { ObjectivesDialog } from '../components/ObjectivesDialog'
import { ScoreCell } from '../components/ScoreCell'
import { WeightsDialog } from '../components/WeightsDialog'
import { gradingErrorText } from '../errors'
import '../i18n'

type Action = 'publish' | 'unpublish' | 'generate' | 'ai'

const averageOf = (row: GradeBookRow, component: GradeComponent) => row.components.find((c) => c.component === component)?.average ?? null

function ResultBadge({ row }: { row: GradeBookRow }) {
  const { t } = useTranslation('grading')

  if (row.isPassing === null) {
    return <span className="text-muted-foreground">{t('book.noGrade')}</span>
  }

  return <Badge variant={row.isPassing ? 'success' : 'destructive'}>{row.isPassing ? t('book.passing') : t('book.below')}</Badge>
}

interface GradeBookViewProps {
  book: GradeBook
  objectiveId: string
  onObjectiveChange: (id: string) => void
}

function GradeBookView({ book, objectiveId, onObjectiveChange }: GradeBookViewProps) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const publish = usePublishGrades()
  const unpublish = useUnpublishGrades()
  const generate = useGenerateReportCards()
  const draftAll = useDraftClassroomDescriptions()
  const objectives = useLearningObjectives(book.subjectId)
  const [objectivesOpen, setObjectivesOpen] = useState(false)
  const [editing, setEditing] = useState<GradeBookRow | null>(null)
  const [weightsOpen, setWeightsOpen] = useState(false)
  const [action, setAction] = useState<Action | null>(null)
  const [actionError, setActionError] = useState<string>()

  const target = { classroomId: book.classroomId, termId: book.termId }
  const pending = publish.isPending || unpublish.isPending || generate.isPending || draftAll.isPending

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
    } else if (action === 'generate') {
      generate.mutate(target, { onSuccess: (result) => done(t('book.generate.done', { count: result.count })), onError: fail })
    } else {
      draftAll.mutate(
        { ...target, subjectId: book.subjectId },
        {
          onSuccess: (result) => {
            const notes = [
              result.alreadyHadText > 0 && t('book.ai.skipped', { count: result.alreadyHadText }),
              result.noScores > 0 && t('book.ai.noScores', { count: result.noScores }),
              result.failed > 0 && t('book.ai.failed', { count: result.failed }),
              result.stoppedBy && t('book.ai.stopped', { reason: t(`errors.${result.stoppedBy}`, { defaultValue: result.stoppedBy }) }),
            ].filter(Boolean)
            setAction(null)
            showToast({
              kind: result.stoppedBy || result.failed > 0 ? 'error' : 'success',
              title: t('book.ai.done', { drafted: result.drafted }),
              body: notes.join(' ') || undefined,
            })
          },
          onError: fail,
        },
      )
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
          learningObjectiveId={objectiveId || undefined}
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
        <div className="flex items-center justify-end gap-2">
          {r.descriptionStatus === 'Draft' && (
            <Badge variant="warning">{r.descriptionIsAiDrafted ? t('book.aiStatus.draftAi') : t('book.aiStatus.draft')}</Badge>
          )}
          <Button variant="outline" size="sm" onClick={() => setEditing(r)}>
            {t('book.actions.text')}
            <span className="sr-only">: {r.studentName}</span>
          </Button>
        </div>
      ),
    },
  ]

  const confirmCopy = action ? t(`book.${action}.title`) : ''
  const confirmLabel = action === 'ai' ? t('book.ai.confirm') : confirmCopy

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
          <Button variant="outline" onClick={() => setObjectivesOpen(true)}>
            {t('book.actions.objectives')}
          </Button>
          <Button variant="outline" disabled={book.isPublished} onClick={() => setAction('ai')}>
            <Sparkles aria-hidden="true" />
            {t('book.actions.draftAi')}
          </Button>
          <Button variant="secondary" disabled={!book.isPublished} onClick={() => setAction('generate')}>
            {t('book.actions.generate')}
          </Button>
        </div>
      </div>

      {objectives.data && objectives.data.length > 0 && (
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex min-w-48 flex-col gap-1.5 text-sm font-medium">
            {t('book.objective.label')}
            <Select value={objectiveId} onChange={(event) => onObjectiveChange(event.target.value)}>
              <option value="">{t('book.objective.overall')}</option>
              {objectives.data.map((objective) => (
                <option key={objective.id} value={objective.id}>
                  {objective.code} - {objective.description}
                </option>
              ))}
            </Select>
          </label>
          {objectiveId && <p className="text-sm text-muted-foreground">{t('book.objective.hint')}</p>}
        </div>
      )}

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
      {objectivesOpen && (
        <ObjectivesDialog
          subjectId={book.subjectId}
          onClose={() => {
            setObjectivesOpen(false)
            if (objectiveId && !objectives.data?.some((o) => o.id === objectiveId)) {
              onObjectiveChange('')
            }
          }}
        />
      )}
      {editing && (
        <DescriptionDialog
          studentId={editing.studentId}
          studentName={editing.studentName}
          subjectId={book.subjectId}
          termId={book.termId}
          initial={editing.description}
          initialStatus={editing.descriptionStatus}
          initialIsAiDrafted={editing.descriptionIsAiDrafted}
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
        confirmLabel={confirmLabel}
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
  const [objectiveId, setObjectiveId] = useState('')

  const termId = chosenTerm ?? terms.data?.find((term) => term.isActive)?.id ?? ''
  const [classroomId = '', subjectId = ''] = choice.split('|')
  const params = classroomId && subjectId && termId ? { classroomId, subjectId, termId, ...(objectiveId ? { learningObjectiveId: objectiveId } : {}) } : null
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
            <Select
              value={choice}
              onChange={(event) => {
                setChoice(event.target.value)
                setObjectiveId('')
              }}
            >
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
        <GradeBookView key={`${params.classroomId}|${params.subjectId}|${params.termId}`} book={book.data} objectiveId={objectiveId} onObjectiveChange={setObjectiveId} />
      )}
    </div>
  )
}
