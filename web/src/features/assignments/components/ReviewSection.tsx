import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { SubmissionRowDto } from '@/api/generated/model'
import { formatDateTime } from '@/shared/lib/format'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { EmptyState, ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useGradeSubmission, useSubmission, useSubmissions } from '../api/assignments'
import { AssignmentStatusBadge } from './AssignmentStatusBadge'
import { SubmissionSummary } from './SubmissionPanel'
import '../i18n'

/** The teacher opens one student's work, reads it, and gives a score with feedback. */
function ReviewDialog({ row, canScore, onClose }: { row: SubmissionRowDto; canScore: boolean; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError } = useSubmission(row.submissionId)
  const grade = useGradeSubmission()
  const [score, setScore] = useState(row.score === null ? '' : String(row.score))
  const [feedback, setFeedback] = useState<string>()
  const [problem, setProblem] = useState<Problem>()

  function save() {
    setProblem(undefined)
    grade.mutate(
      { submissionId: row.submissionId!, score: Number(score), feedback: (feedback ?? data?.feedback ?? '').trim() || null },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('review.saved') })
          onClose()
        },
        onError: (e) => setProblem(parseProblem(e)),
      },
    )
  }

  const generalError = problem && Object.keys(problem.fieldErrors).length === 0

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('review.dialogTitle', { name: row.studentName })}</DialogTitle>
        </DialogHeader>
        {isPending ? (
          <Skeleton className="h-40" />
        ) : isError ? (
          <ErrorNotice message={t('review.error')} />
        ) : (
          <div className="flex flex-col gap-4">
            <SubmissionSummary submission={{ ...data, score: null, feedback: null }} />
            {!data.textBody && !data.hasFile && <p className="text-sm text-muted-foreground">{t('review.noText')}</p>}
            {canScore && (
              <form
                noValidate
                className="flex flex-col gap-4 border-t border-border pt-4"
                onSubmit={(event) => {
                  event.preventDefault()
                  save()
                }}
              >
                {generalError && <Alert tone="error" message={problem?.title ?? tCommon('errors.generic')} />}
                <FormField label={t('review.score')} required error={fieldError(problem, 'score')}>
                  <Input type="number" min={0} max={100} step="0.01" value={score} onChange={(event) => setScore(event.target.value)} className="max-w-32" />
                </FormField>
                <FormField label={t('review.feedback')} error={fieldError(problem, 'feedback')}>
                  <Textarea rows={3} maxLength={2000} value={feedback ?? data.feedback ?? ''} onChange={(event) => setFeedback(event.target.value)} />
                </FormField>
                <DialogFooter>
                  <Button type="button" variant="outline" onClick={onClose}>
                    {tCommon('actions.cancel')}
                  </Button>
                  <Button type="submit" disabled={grade.isPending || score === ''}>
                    {grade.isPending ? tCommon('actions.saving') : t('review.save')}
                  </Button>
                </DialogFooter>
              </form>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

/** Every student of the assignment's classrooms, handed in or not, with the way to review and score the work. */
export function ReviewSection({ assignmentId, canScore }: { assignmentId: string; canScore: boolean }) {
  const { t } = useTranslation('assignments')
  const { data, isPending, isError, refetch } = useSubmissions(assignmentId)
  const [reviewing, setReviewing] = useState<SubmissionRowDto | null>(null)

  const columns: Column<SubmissionRowDto>[] = [
    { key: 'student', header: t('review.columns.student'), render: (r) => <span className="font-medium">{r.studentName}</span> },
    { key: 'classroom', header: t('review.columns.classroom'), render: (r) => r.classroomName },
    { key: 'status', header: t('review.columns.status'), render: (r) => <AssignmentStatusBadge status={r.status} /> },
    { key: 'submitted', header: t('review.columns.submitted'), render: (r) => (r.submittedAt ? formatDateTime(r.submittedAt) : '—') },
    { key: 'score', header: t('review.columns.score'), className: 'tabular-nums', render: (r) => r.score ?? '—' },
    {
      key: 'actions',
      header: '',
      className: 'text-right',
      render: (r) =>
        r.submissionId && (
          <Button variant="outline" size="sm" onClick={() => setReviewing(r)}>
            {t('review.review')}
            <span className="sr-only">: {t('review.reviewFor', { name: r.studentName })}</span>
          </Button>
        ),
    },
  ]

  const submitted = data?.filter((r) => r.submissionId).length ?? 0
  const graded = data?.filter((r) => r.score !== null).length ?? 0

  return (
    <section aria-labelledby="review-title" className="flex flex-col gap-3">
      <h2 id="review-title" className="text-lg font-semibold">
        {t('review.title')}
      </h2>
      {isError ? (
        <ErrorNotice message={t('review.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          {data && <p className="text-sm text-muted-foreground">{t('review.summary', { submitted, total: data.length, graded })}</p>}
          <DataTable
            caption={t('review.caption')}
            columns={columns}
            rows={data}
            rowKey={(r) => r.studentId}
            isLoading={isPending}
            empty={<EmptyState title={t('review.caption')} />}
          />
        </>
      )}
      {reviewing && <ReviewDialog row={reviewing} canScore={canScore} onClose={() => setReviewing(null)} />}
    </section>
  )
}
