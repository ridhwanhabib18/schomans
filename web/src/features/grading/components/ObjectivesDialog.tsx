import { Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateObjective, useDeleteObjective, useLearningObjectives, useUpdateObjective } from '../api/grading'
import type { LearningObjective } from '../api/types'
import { gradingErrorText } from '../errors'
import '../i18n'

interface ObjectivesDialogProps {
  subjectId: string
  onClose: () => void
}

const CODE_MAX = 20
const DESCRIPTION_MAX = 300

/** The teacher of a subject keeps its learning objectives: the goals that scores are linked to and the AI describes. */
export function ObjectivesDialog({ subjectId, onClose }: ObjectivesDialogProps) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const objectives = useLearningObjectives(subjectId)
  const create = useCreateObjective(subjectId)
  const update = useUpdateObjective()
  const remove = useDeleteObjective()
  const [editing, setEditing] = useState<LearningObjective | null>(null)
  const [code, setCode] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string>()

  const pending = create.isPending || update.isPending || remove.isPending
  const fail = (e: unknown) => setError(gradingErrorText(e, t, tCommon('errors.generic')))

  function reset() {
    setEditing(null)
    setCode('')
    setDescription('')
  }

  function save() {
    setError(undefined)
    const input = { code: code.trim(), description: description.trim() }
    const done = () => {
      showToast({ kind: 'success', title: t('objectives.saved') })
      reset()
    }

    if (editing) {
      update.mutate({ id: editing.id, input }, { onSuccess: done, onError: fail })
    } else {
      create.mutate(input, { onSuccess: done, onError: fail })
    }
  }

  function startEdit(objective: LearningObjective) {
    setError(undefined)
    setEditing(objective)
    setCode(objective.code)
    setDescription(objective.description)
  }

  function removeObjective(objective: LearningObjective) {
    setError(undefined)
    remove.mutate(objective.id, {
      onSuccess: () => {
        showToast({ kind: 'success', title: t('objectives.removed') })
        if (editing?.id === objective.id) {
          reset()
        }
      },
      onError: fail,
    })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('objectives.title')}</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">{t('objectives.body')}</p>
        {error && <Alert tone="error" message={error} />}

        {objectives.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : objectives.isError ? (
          <Alert tone="error" message={t('objectives.error')} />
        ) : objectives.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('objectives.none')}</p>
        ) : (
          <ul className="flex max-h-64 flex-col divide-y overflow-y-auto rounded-md border">
            {objectives.data.map((objective) => (
              <li key={objective.id} className="flex items-start gap-3 p-3">
                <span className="w-16 shrink-0 font-medium tabular-nums">{objective.code}</span>
                <span className="min-w-0 flex-1 text-sm">{objective.description}</span>
                <Button variant="ghost" size="icon" disabled={pending} onClick={() => startEdit(objective)}>
                  <Pencil aria-hidden="true" className="size-4" />
                  <span className="sr-only">{t('objectives.edit', { code: objective.code })}</span>
                </Button>
                <Button variant="ghost" size="icon" disabled={pending} onClick={() => removeObjective(objective)}>
                  <Trash2 aria-hidden="true" className="size-4" />
                  <span className="sr-only">{t('objectives.remove', { code: objective.code })}</span>
                </Button>
              </li>
            ))}
          </ul>
        )}

        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            save()
          }}
        >
          <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
            <FormField label={t('objectives.code')}>
              <Input maxLength={CODE_MAX} value={code} onChange={(event) => setCode(event.target.value)} />
            </FormField>
            <FormField label={t('objectives.description')}>
              <Input maxLength={DESCRIPTION_MAX} value={description} onChange={(event) => setDescription(event.target.value)} />
            </FormField>
          </div>
          <DialogFooter>
            {editing && (
              <Button type="button" variant="outline" onClick={reset}>
                {tCommon('actions.cancel')}
              </Button>
            )}
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.close')}
            </Button>
            <Button type="submit" disabled={pending || code.trim() === '' || description.trim() === ''}>
              {editing ? t('objectives.save') : t('objectives.add')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
