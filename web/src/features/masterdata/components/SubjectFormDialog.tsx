import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { SubjectDto } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateSubject, useUpdateSubject } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

export function SubjectFormDialog({ subject, onClose }: { subject?: SubjectDto; onClose: () => void }) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateSubject()
  const update = useUpdateSubject()
  const [name, setName] = useState(subject?.name ?? '')
  const [score, setScore] = useState(subject ? String(subject.minimumPassingScore) : '')
  const [problem, setProblem] = useState<Problem>()

  function submit() {
    setProblem(undefined)
    const minimumPassingScore = Number(score)
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }

    if (subject) {
      update.mutate(
        { id: subject.id, command: { id: subject.id, name, minimumPassingScore, rowVersion: subject.rowVersion } },
        { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) },
      )
    } else {
      create.mutate({ name, minimumPassingScore }, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    }
  }

  return (
    <FormDialog
      title={subject ? t('subjects.form.editTitle') : t('subjects.form.newTitle')}
      problem={problem}
      errorPrefix="subjects.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      <FormField label={t('subjects.form.name')} required error={fieldError(problem, 'name')}>
        <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
      </FormField>
      <FormField label={t('subjects.form.score')} required hint={t('subjects.form.scoreHint')} error={fieldError(problem, 'minimumPassingScore')}>
        <Input type="number" min={0} max={100} step="0.01" value={score} onChange={(event) => setScore(event.target.value)} />
      </FormField>
    </FormDialog>
  )
}
