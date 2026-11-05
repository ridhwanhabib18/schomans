import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useRubric, useSetRubric, type RubricCriterion } from '../api/feedback'
import { assignmentErrorText } from '../errors'
import '../i18n'

export const MAX_CRITERIA = 6

const blank = (): RubricCriterion => ({ name: '', description: '', maxPoints: 10 })

function RubricForm({ assignmentId, initial, onClose }: { assignmentId: string; initial: RubricCriterion[]; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSetRubric(assignmentId)
  const [criteria, setCriteria] = useState<RubricCriterion[]>(initial.length > 0 ? initial : [blank()])
  const [error, setError] = useState<string>()

  const change = (index: number, patch: Partial<RubricCriterion>) =>
    setCriteria((current) => current.map((c, i) => (i === index ? { ...c, ...patch } : c)))
  const total = criteria.reduce((sum, c) => sum + (Number.isFinite(c.maxPoints) ? c.maxPoints : 0), 0)
  const complete = criteria.every((c) => c.name.trim() !== '' && c.maxPoints >= 1 && c.maxPoints <= 100)

  function submit() {
    setError(undefined)
    save.mutate(
      criteria.map((c) => ({ name: c.name.trim(), description: c.description.trim(), maxPoints: c.maxPoints })),
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('rubric.saved') })
          onClose()
        },
        onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        submit()
      }}
    >
      <p className="text-sm text-muted-foreground">{t('rubric.body')}</p>
      {error && <Alert tone="error" message={error} />}
      <ul className="flex flex-col gap-4">
        {criteria.map((criterion, index) => (
          <li key={index} className="flex min-w-0 flex-col gap-3 rounded-lg border border-border p-3">
            <div className="flex flex-wrap items-end gap-3">
              <FormField label={t('rubric.name', { number: index + 1 })} required className="min-w-0 flex-1">
                <Input value={criterion.name} maxLength={60} onChange={(event) => change(index, { name: event.target.value })} />
              </FormField>
              <FormField label={t('rubric.maxPoints', { number: index + 1 })} required>
                <Input
                  type="number"
                  min={1}
                  max={100}
                  value={Number.isFinite(criterion.maxPoints) ? criterion.maxPoints : ''}
                  onChange={(event) => change(index, { maxPoints: event.target.value === '' ? Number.NaN : Number(event.target.value) })}
                  className="w-24"
                />
              </FormField>
              {criteria.length > 1 && (
                <Button type="button" variant="outline" size="sm" onClick={() => setCriteria((current) => current.filter((_, i) => i !== index))}>
                  <Trash2 aria-hidden="true" />
                  {t('rubric.remove')}
                  <span className="sr-only">: {t('rubric.criterion', { number: index + 1 })}</span>
                </Button>
              )}
            </div>
            <FormField label={t('rubric.description', { number: index + 1 })}>
              <Input value={criterion.description} maxLength={300} onChange={(event) => change(index, { description: event.target.value })} />
            </FormField>
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Button type="button" variant="outline" size="sm" disabled={criteria.length >= MAX_CRITERIA} onClick={() => setCriteria((current) => [...current, blank()])}>
          <Plus aria-hidden="true" />
          {t('rubric.add')}
        </Button>
        <span className="text-sm text-muted-foreground">{t('rubric.total', { total })}</span>
      </div>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onClose}>
          {tCommon('actions.cancel')}
        </Button>
        <Button type="submit" disabled={save.isPending || !complete}>
          {save.isPending ? tCommon('actions.saving') : t('rubric.save')}
        </Button>
      </DialogFooter>
    </form>
  )
}

/** The teacher of an assignment sets the criteria the AI judges the work against (AI-05). */
export function RubricDialog({ assignmentId, onClose }: { assignmentId: string; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { data, isPending, isError } = useRubric(assignmentId)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('rubric.title')}</DialogTitle>
        </DialogHeader>
        {isPending ? (
          <Skeleton className="h-40" />
        ) : isError ? (
          <ErrorNotice message={t('rubric.error')} />
        ) : (
          <RubricForm assignmentId={assignmentId} initial={data.criteria} onClose={onClose} />
        )}
      </DialogContent>
    </Dialog>
  )
}
