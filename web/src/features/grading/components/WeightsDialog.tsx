import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useGradeWeights, useSetGradeWeights } from '../api/grading'
import { GRADE_COMPONENTS, type GradeComponent, type GradeWeights } from '../api/types'
import { gradingErrorText } from '../errors'
import '../i18n'

function WeightsForm({ subjectId, weights, onClose }: { subjectId: string; weights: GradeWeights; onClose: () => void }) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSetGradeWeights()
  const [values, setValues] = useState<Record<GradeComponent, string>>(
    () => Object.fromEntries(weights.weights.map((w) => [w.component, String(w.weightPercent)])) as Record<GradeComponent, string>,
  )
  const [error, setError] = useState<string>()

  const total = GRADE_COMPONENTS.reduce((sum, component) => sum + (Number(values[component]) || 0), 0)
  const rounded = Math.round(total * 100) / 100
  const valid = rounded === 100 && GRADE_COMPONENTS.every((component) => values[component] !== '' && Number(values[component]) >= 0)

  function submit() {
    setError(undefined)
    save.mutate(
      { subjectId, weights: GRADE_COMPONENTS.map((component) => ({ component, weightPercent: Number(values[component]) })) },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('weights.saved') })
          onClose()
        },
        onError: (e) => setError(gradingErrorText(e, t, tCommon('errors.generic'))),
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
      {weights.isDefault && <Alert tone="info" message={t('weights.default')} />}
      {error && <Alert tone="error" message={error} />}
      <div className="grid grid-cols-2 gap-4">
        {GRADE_COMPONENTS.map((component) => (
          <FormField key={component} label={t(`components.${component}`)} className="min-w-0">
            <Input
              type="number"
              min={0}
              max={100}
              step="1"
              className="tabular-nums"
              value={values[component]}
              onChange={(event) => setValues((current) => ({ ...current, [component]: event.target.value }))}
            />
          </FormField>
        ))}
      </div>
      <p className={rounded === 100 ? 'text-sm text-muted-foreground' : 'text-sm font-medium text-destructive'} aria-live="polite">
        {t('weights.total', { total: rounded })}
        {rounded !== 100 && ` - ${t('weights.mustBe100')}`}
      </p>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onClose}>
          {tCommon('actions.cancel')}
        </Button>
        <Button type="submit" disabled={!valid || save.isPending}>
          {save.isPending ? tCommon('actions.saving') : t('weights.save')}
        </Button>
      </DialogFooter>
    </form>
  )
}

/** The teacher of a subject sets how the components count in its final grade. */
export function WeightsDialog({ subjectId, onClose }: { subjectId: string; onClose: () => void }) {
  const { t } = useTranslation('grading')
  const { data, isPending, isError } = useGradeWeights(subjectId)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('weights.title')}</DialogTitle>
          <DialogDescription>{t('weights.body')}</DialogDescription>
        </DialogHeader>
        {isPending ? <Skeleton className="h-40" /> : isError ? <Alert tone="error" message={t('weights.error')} /> : <WeightsForm subjectId={subjectId} weights={data} onClose={onClose} />}
      </DialogContent>
    </Dialog>
  )
}
