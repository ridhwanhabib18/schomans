import { Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Input, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcceptFeedback, useDraftFeedback, useFeedbackDraft, useRejectFeedback, type CriterionEdit, type FeedbackDraft } from '../api/feedback'
import { assignmentErrorText } from '../errors'
import '../i18n'

/** The points of all criteria as a score from 0 to 100, with one decimal: the same rule as the API. */
export function scoreOf(criteria: { maxPoints: number; points: number }[]): number {
  const max = criteria.reduce((sum, c) => sum + c.maxPoints, 0)
  return max === 0 ? 0 : Math.round((criteria.reduce((sum, c) => sum + c.points, 0) * 1000) / max) / 10
}

function DraftEditor({ draft, onAccepted }: { draft: FeedbackDraft; onAccepted: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const draftAgain = useDraftFeedback()
  const accept = useAcceptFeedback()
  const reject = useRejectFeedback()
  const [edits, setEdits] = useState<CriterionEdit[]>(draft.criteria.map((c) => ({ points: c.points, reason: c.reason })))
  const [suggestion, setSuggestion] = useState(draft.suggestion)
  const [error, setError] = useState<string>()

  const fail = (e: unknown) => setError(assignmentErrorText(e, t, tCommon('errors.generic')))
  const busy = accept.isPending || reject.isPending || draftAgain.isPending
  const valid = draft.criteria.every((c, i) => Number.isInteger(edits[i]?.points) && edits[i]!.points >= 0 && edits[i]!.points <= c.maxPoints && edits[i]!.reason.trim() !== '') && suggestion.trim() !== ''
  const score = scoreOf(draft.criteria.map((c, i) => ({ maxPoints: c.maxPoints, points: Number.isFinite(edits[i]?.points) ? edits[i]!.points : 0 })))

  return (
    <div className="flex flex-col gap-4">
      {draft.status === 'Rejected' && <Alert tone="info" message={t('feedback.rejectedNote')} />}
      {error && <Alert tone="error" message={error} />}
      <ul className="flex flex-col gap-3">
        {draft.criteria.map((criterion, index) => (
          <li key={criterion.name} className="flex min-w-0 flex-col gap-2 rounded-lg border border-border p-3">
            <div className="flex flex-wrap items-end gap-3">
              <span className="min-w-0 flex-1 font-medium">{criterion.name}</span>
              <FormField label={t('feedback.points', { name: criterion.name, max: criterion.maxPoints })}>
                <Input
                  type="number"
                  min={0}
                  max={criterion.maxPoints}
                  step={1}
                  value={Number.isFinite(edits[index]?.points) ? edits[index]!.points : ''}
                  onChange={(event) =>
                    setEdits((current) => current.map((e, i) => (i === index ? { ...e, points: event.target.value === '' ? Number.NaN : Number(event.target.value) } : e)))
                  }
                  className="w-24"
                />
              </FormField>
            </div>
            <FormField label={t('feedback.reason', { name: criterion.name })}>
              <Input
                value={edits[index]?.reason ?? ''}
                maxLength={180}
                onChange={(event) => setEdits((current) => current.map((e, i) => (i === index ? { ...e, reason: event.target.value } : e)))}
              />
            </FormField>
          </li>
        ))}
      </ul>
      <FormField label={t('feedback.suggestion')}>
        <Textarea rows={2} maxLength={300} value={suggestion} onChange={(event) => setSuggestion(event.target.value)} />
      </FormField>
      <p className="text-sm font-medium">{t('feedback.score', { score })}</p>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={busy || !valid}
          onClick={() => {
            setError(undefined)
            accept.mutate(
              { submissionId: draft.submissionId, criteria: edits.map((e) => ({ points: e.points, reason: e.reason.trim() })), suggestion: suggestion.trim() },
              {
                onSuccess: () => {
                  showToast({ kind: 'success', title: t('feedback.accepted') })
                  onAccepted()
                },
                onError: fail,
              },
            )
          }}
        >
          {accept.isPending ? tCommon('actions.saving') : t('feedback.accept')}
        </Button>
        {draft.status === 'Draft' && (
          <Button
            type="button"
            variant="outline"
            disabled={busy}
            onClick={() => {
              setError(undefined)
              reject.mutate(draft.submissionId, { onSuccess: () => showToast({ kind: 'success', title: t('feedback.rejected') }), onError: fail })
            }}
          >
            {t('feedback.reject')}
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          disabled={busy}
          onClick={() => {
            setError(undefined)
            draftAgain.mutate(draft.submissionId, { onError: fail })
          }}
        >
          <Sparkles aria-hidden="true" />
          {draftAgain.isPending ? t('feedback.drafting') : t('feedback.again')}
        </Button>
      </div>
    </div>
  )
}

/**
 * AI-05 inside the review of one piece of work: the teacher asks for a draft against the rubric, edits the points and the texts, and
 * accepts it (that is what scores the work) or rejects it. A draft never scores the work by itself.
 */
export function FeedbackPanel({ submissionId, hasText, isScored, onAccepted }: { submissionId: string; hasText: boolean; isScored: boolean; onAccepted: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const { data: draft, isPending } = useFeedbackDraft(submissionId)
  const create = useDraftFeedback()
  const [error, setError] = useState<string>()

  if (isPending) {
    return <Skeleton className="h-16" />
  }

  if (draft?.status === 'Accepted') {
    return (
      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border p-3 text-sm">
        <Badge variant="success">{t('feedback.status.Accepted')}</Badge>
        {t('feedback.acceptedSummary', { score: draft.score })}
      </div>
    )
  }

  if (draft) {
    return (
      <section aria-labelledby="feedback-title" className="flex flex-col gap-3 border-t border-border pt-4">
        <h3 id="feedback-title" className="flex items-center gap-2 font-semibold">
          <Sparkles aria-hidden="true" className="size-4 text-primary" />
          {t('feedback.title')}
          <Badge variant={draft.status === 'Rejected' ? 'muted' : 'warning'}>{t(`feedback.status.${draft.status}`)}</Badge>
        </h3>
        <DraftEditor key={`${draft.status}-${draft.criteria.map((c) => c.points).join(',')}-${draft.suggestion}`} draft={draft} onAccepted={onAccepted} />
      </section>
    )
  }

  if (isScored) {
    return null
  }

  return (
    <section aria-labelledby="feedback-title" className="flex flex-col gap-2 border-t border-border pt-4">
      <h3 id="feedback-title" className="flex items-center gap-2 font-semibold">
        <Sparkles aria-hidden="true" className="size-4 text-primary" />
        {t('feedback.title')}
      </h3>
      {error && <Alert tone="error" message={error} />}
      {hasText ? (
        <>
          <p className="text-sm text-muted-foreground">{t('feedback.intro')}</p>
          <div>
            <Button
              type="button"
              variant="outline"
              disabled={create.isPending}
              onClick={() => {
                setError(undefined)
                create.mutate(submissionId, { onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))) })
              }}
            >
              <Sparkles aria-hidden="true" />
              {create.isPending ? t('feedback.drafting') : t('feedback.draft')}
            </Button>
          </div>
        </>
      ) : (
        <p className="text-sm text-muted-foreground">{t('feedback.needsText')}</p>
      )}
    </section>
  )
}
