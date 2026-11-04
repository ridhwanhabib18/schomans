import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useGradeQuiz, useQuiz, type Quiz } from '../api/quizzes'
import { assignmentErrorText } from '../errors'
import { QuestionResult } from './QuizResult'
import '../i18n'

/** Points for the short answers: every one needs a number from 0 to what the question is worth before the quiz can be scored. */
function Grading({ assignmentId, studentId, quiz, canScore, onClose }: { assignmentId: string; studentId: string; quiz: Quiz; canScore: boolean; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const grade = useGradeQuiz(assignmentId)
  const short = quiz.questions.filter((q) => q.type === 'ShortAnswer')
  const [points, setPoints] = useState<Record<string, string>>(() => Object.fromEntries(short.map((q) => [q.questionId, q.pointsAwarded === null ? '' : String(q.pointsAwarded)])))
  const [error, setError] = useState<string>()
  const valid = short.every((q) => {
    const value = points[q.questionId] ?? ''
    return value.trim() !== '' && Number.isFinite(Number(value)) && Number(value) >= 0 && Number(value) <= q.points
  })

  if (short.length === 0) {
    return null
  }

  return (
    <section aria-labelledby="short-title" className="flex flex-col gap-3 border-t border-border pt-4">
      <h3 id="short-title" className="font-semibold">
        {t('quiz.grading.title')}
      </h3>
      <p className="text-sm text-muted-foreground">{t('quiz.grading.intro')}</p>
      {error && <Alert tone="error" message={error} />}
      {short.map((q) => (
        <FormField key={q.questionId} label={t('quiz.grading.points', { number: q.order, max: q.points })}>
          <Input
            type="number"
            min={0}
            max={q.points}
            step={0.5}
            disabled={!canScore}
            value={points[q.questionId] ?? ''}
            className="w-28"
            onChange={(event) => setPoints((current) => ({ ...current, [q.questionId]: event.target.value }))}
          />
        </FormField>
      ))}
      {canScore && (
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {tCommon('actions.cancel')}
          </Button>
          <Button
            type="button"
            disabled={!valid || grade.isPending}
            onClick={() => {
              setError(undefined)
              grade.mutate(
                { studentId, points: short.map((q) => ({ questionId: q.questionId, points: Number(points[q.questionId]) })) },
                {
                  onSuccess: () => {
                    showToast({ kind: 'success', title: t('quiz.grading.saved') })
                    onClose()
                  },
                  onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))),
                },
              )
            }}
          >
            {grade.isPending ? tCommon('actions.saving') : t('quiz.grading.save')}
          </Button>
        </DialogFooter>
      )}
    </section>
  )
}

/** The teacher opens the attempt of one student: the answers next to the key, and the points for the short answers. */
export function QuizAttemptDialog({ assignmentId, studentId, studentName, canScore, onClose }: { assignmentId: string; studentId: string; studentName: string; canScore: boolean; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { data, isPending, isError, refetch } = useQuiz(assignmentId, studentId)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('review.dialogTitle', { name: studentName })}</DialogTitle>
        </DialogHeader>
        {isPending ? (
          <Skeleton className="h-40" />
        ) : isError ? (
          <ErrorNotice message={t('quiz.error')} onRetry={() => void refetch()} />
        ) : !data.attempt ? (
          <p className="text-sm text-muted-foreground">{t('quiz.noAttempt')}</p>
        ) : (
          <div className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <span>{t('submit.submittedAt', { date: formatDateTime(data.attempt.submittedAt) })}</span>
              {data.attempt.status === 'Scored' ? (
                <>
                  <Badge variant="success">{t('quiz.status.Scored')}</Badge>
                  <span className="font-semibold tabular-nums">{t('quiz.score', { score: data.attempt.score })}</span>
                </>
              ) : (
                <Badge variant="warning">{t('quiz.waitingForTeacher', { count: data.attempt.shortAnswersToGrade })}</Badge>
              )}
            </div>
            <ol className="flex flex-col gap-3">
              {data.questions.map((q) => (
                <QuestionResult key={q.questionId} question={q} />
              ))}
            </ol>
            <Grading key={data.attempt.submissionId}assignmentId={assignmentId} studentId={studentId} quiz={data} canScore={canScore} onClose={onClose} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
