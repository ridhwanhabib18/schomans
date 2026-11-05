import { useTranslation } from 'react-i18next'
import { Badge } from '@/shared/ui/badge'
import type { QuizQuestion } from '../api/quizzes'
import '../i18n'

/**
 * One question with the answer given. When the key is known (the teacher always, a student after the due date) it also shows the right
 * answer, the explanation and the points. Meaning is in the words, never in colour alone.
 */
export function QuestionResult({ question }: { question: QuizQuestion }) {
  const { t } = useTranslation('assignments')
  const chosen = question.myAnswer === null ? null : Number(question.myAnswer)
  const right = question.answerKey !== null && question.type === 'MultipleChoice' ? Number(question.answerKey) : null

  return (
    <li className="flex min-w-0 flex-col gap-2 rounded-lg border border-border p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="font-medium">
          {question.order}. {question.body}
        </p>
        <span className="flex items-center gap-2 text-sm tabular-nums text-muted-foreground">
          {question.pointsAwarded !== null ? t('quiz.pointsOf', { earned: question.pointsAwarded, total: question.points }) : t('quiz.points', { count: question.points })}
        </span>
      </div>

      {question.type === 'MultipleChoice' ? (
        <ul className="flex flex-col gap-1 text-sm">
          {question.options.map((option, index) => (
            <li key={index} className="flex flex-wrap items-center gap-2">
              <span>{option}</span>
              {chosen === index && <Badge variant="muted">{t('quiz.yourAnswer')}</Badge>}
              {right === index && <Badge variant="success">{t('quiz.rightAnswer')}</Badge>}
            </li>
          ))}
          {chosen === null && <li className="text-muted-foreground">{t('quiz.noAnswer')}</li>}
        </ul>
      ) : (
        <div className="flex flex-col gap-1 text-sm">
          <p>
            <span className="font-medium">{t('quiz.yourAnswer')}: </span>
            {question.myAnswer ? <span className="whitespace-pre-wrap">{question.myAnswer}</span> : <span className="text-muted-foreground">{t('quiz.noAnswer')}</span>}
          </p>
          {question.answerKey !== null && (
            <p>
              <span className="font-medium">{t('quiz.modelAnswer')}: </span>
              <span className="whitespace-pre-wrap">{question.answerKey}</span>
            </p>
          )}
        </div>
      )}

      {question.explanation && (
        <p className="text-sm text-muted-foreground">
          <span className="font-medium">{t('quiz.explanation')}: </span>
          {question.explanation}
        </p>
      )}
    </li>
  )
}
