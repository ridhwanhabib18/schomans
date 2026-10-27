import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TeacherDto } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateTeacher, useUpdateTeacher } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

export function TeacherFormDialog({ teacher, onClose }: { teacher?: TeacherDto; onClose: () => void }) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateTeacher()
  const update = useUpdateTeacher()
  const [fullName, setFullName] = useState(teacher?.fullName ?? '')
  const [email, setEmail] = useState(teacher?.email ?? '')
  const [nip, setNip] = useState(teacher?.nip ?? '')
  const [problem, setProblem] = useState<Problem>()
  const identityLocked = teacher?.hasAccount === true

  function submit() {
    setProblem(undefined)
    const fields = { fullName, email, nip }
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }

    if (teacher) {
      update.mutate({ id: teacher.id, command: { ...fields, id: teacher.id, rowVersion: teacher.rowVersion } }, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    } else {
      create.mutate(fields, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    }
  }

  return (
    <FormDialog
      title={teacher ? t('teachers.form.editTitle') : t('teachers.form.newTitle')}
      problem={problem}
      errorPrefix="teachers.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      {identityLocked && <p className="rounded-md bg-muted p-3 text-sm">{t('common.managedByAccount')}</p>}
      <FormField label={t('teachers.form.fullName')} required error={fieldError(problem, 'fullName')}>
        <Input value={fullName} disabled={identityLocked} maxLength={200} onChange={(event) => setFullName(event.target.value)} />
      </FormField>
      <FormField label={t('teachers.form.email')} required hint={t('teachers.form.emailHint')} error={fieldError(problem, 'email')}>
        <Input type="email" value={email} disabled={identityLocked} maxLength={256} onChange={(event) => setEmail(event.target.value)} />
      </FormField>
      <FormField label={t('teachers.form.nip')} required hint={t('teachers.form.nipHint')} error={fieldError(problem, 'nip')}>
        <Input value={nip} inputMode="numeric" maxLength={18} onChange={(event) => setNip(event.target.value)} />
      </FormField>
    </FormDialog>
  )
}
