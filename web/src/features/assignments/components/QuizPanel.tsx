import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useQuiz, useSubmitQuiz, type Quiz } from '../api/quizzes'
import { assignmentErrorText } from '../errors'
import { QuestionResult } from './QuizResult'
import '../i18n'

/** The quiz as a student takes it: no key, one attempt, handed in after a confirmation. */
function QuizTaker({ quiz }: { quiz: Quiz }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const submit = useSubmitQuiz(quiz.assignmentId)
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string>()
  const unanswered = quiz.questions.filter((q) => (answers[q.questionId] ?? '').trim() === '').length

  function send() {
    setError(undefined)
    submit.mutate(
      quiz.questions.map((q) => ({ questionId: q.questionId, answer: (answers[q.questionId] ?? '').trim() || null })),
      {
        onSuccess: () => {
          setConfirming(false)
          showToast({ kind: 'success', title: t('quiz.handedIn') })
        },
        onError: (e) => {
          setConfirming(false)
          setError(assignmentErrorText(e, t, tCommon('errors.generic')))
        },
      },
    )
  }

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        setConfirming(true)
      }}
    >
      <Alert tone="info" message={t('quiz.onceNote')} />
      {error && <Alert tone="error" message={error} />}
      <ol className="flex flex-col gap-4">
        {quiz.questions.map((q) => (
          <li key={q.questionId}>
            <fieldset className="flex min-w-0 flex-col gap-2 rounded-lg border border-border p-3">
              <legend className="px-1 font-medium">
                {q.order}. {q.body} <span className="text-sm font-normal text-muted-foreground">({t('quiz.points', { count: q.points })})</span>
              </legend>
              {q.type === 'MultipleChoice' ? (
                q.options.map((option, index) => (
                  <label key={index} className="flex items-center gap-2 text-sm">
                    <input
                      type="radio"
                      name={`q-${q.questionId}`}
                      checked={answers[q.questionId] === String(index)}
                      onChange={() => setAnswers((current) => ({ ...current, [q.questionId]: String(index) }))}
                      className="size-4 accent-[var(--primary)]"
                    />
                    {option}
                  </label>
                ))
              ) : (
                <Textarea
                  rows={3}
                  maxLength={500}
                  aria-label={t('quiz.answerFor', { number: q.order })}
                  value={answers[q.questionId] ?? ''}
                  onChange={(event) => setAnswers((current) => ({ ...current, [q.questionId]: event.target.value }))}
                />
              )}
            </fieldset>
          </li>
        ))}
      </ol>
      <div>
        <Button type="submit" disabled={submit.isPending}>
          {submit.isPending ? t('quiz.handingIn') : t('quiz.handIn')}
        </Button>
      </div>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t('quiz.confirmTitle')}
        description={unanswered > 0 ? t('quiz.confirmUnanswered', { count: unanswered }) : t('quiz.confirmBody')}
        confirmLabel={t('quiz.handIn')}
        pending={submit.isPending}
        onConfirm={send}
      />
    </form>
  )
}

function AttemptStatus({ quiz }: { quiz: Quiz }) {
  const { t } = useTranslation('assignments')
  const attempt = quiz.attempt!

  if (attempt.status === 'Submitted') {
    return <Alert tone="info" message={t('quiz.waitForDue', { date: formatDateTime(quiz.dueAt) })} />
  }

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-lg border border-border p-3">
      {attempt.status === 'Scored' ? (
        <>
          <Badge variant="success">{t('quiz.status.Scored')}</Badge>
          <span className="text-lg font-semibold tabular-nums">{t('quiz.score', { score: attempt.score })}</span>
        </>
      ) : (
        <>
          <Badge variant="warning">{t('quiz.status.WaitingForTeacher')}</Badge>
          <span className="text-sm">{t('quiz.waitingForTeacher', { count: attempt.shortAnswersToGrade })}</span>
        </>
      )}
    </div>
  )
}

/**
 * The quiz on the page of an assignment, for a student or for a parent looking at their child: to take while it is open, then the hand-in,
 * and after the due date the score, the key and the explanations. Before the due date nothing of the result is shown.
 */
export function QuizPanel({ assignmentId, studentId, canTake }: { assignmentId: string; studentId?: string; canTake: boolean }) {
  const { t } = useTranslation('assignments')
  const { data, isPending, isError, refetch } = useQuiz(assignmentId, studentId)

  if (isPending) {
    return <Skeleton className="h-40" />
  }

  if (isError) {
    return <ErrorNotice message={t('quiz.error')} onRetry={() => void refetch()} />
  }

  return (
    <section aria-labelledby="quiz-title" className="flex flex-col gap-4 rounded-xl border border-border bg-card p-4">
      <h2 id="quiz-title" className="text-lg font-semibold">
        {canTake ? t('quiz.title') : t('quiz.titleChild')}
      </h2>
      {data.attempt ? (
        <>
          <p className="text-sm text-muted-foreground">{t('submit.submittedAt', { date: formatDateTime(data.attempt.submittedAt) })}</p>
          <AttemptStatus quiz={data} />
        </>
      ) : data.isOpen ? (
        !canTake && <p className="text-sm text-muted-foreground">{t('quiz.childNotYet')}</p>
      ) : (
        <Alert tone="info" message={t('quiz.closedNotTaken')} />
      )}

      {data.attempt === null && data.isOpen && canTake && <QuizTaker quiz={data} />}

      {data.resultsVisible && (
        <ol className="flex flex-col gap-3" aria-label={t('quiz.resultsLabel')}>
          {data.questions.map((q) => (
            <QuestionResult key={q.questionId} question={q} />
          ))}
        </ol>
      )}
    </section>
  )
}
