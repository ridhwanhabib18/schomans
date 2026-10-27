import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StudentDto } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useActiveClassrooms, useCreateStudent, useUpdateStudent } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

interface StudentFormDialogProps {
  /** The student to edit, or undefined to add one. */
  student?: StudentDto
  onClose: () => void
}

export function StudentFormDialog({ student, onClose }: StudentFormDialogProps) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const classrooms = useActiveClassrooms()
  const create = useCreateStudent()
  const update = useUpdateStudent()
  const [fullName, setFullName] = useState(student?.fullName ?? '')
  const [email, setEmail] = useState(student?.email ?? '')
  const [nisn, setNisn] = useState(student?.nisn ?? '')
  const [classroomId, setClassroomId] = useState(student?.classroomId ?? '')
  const [dateOfBirth, setDateOfBirth] = useState(student?.dateOfBirth ?? '')
  const [problem, setProblem] = useState<Problem>()

  const graduated = student?.status === 'Graduated'
  // A student who already signed in is managed by their Keycloak account: the school cannot rename them.
  const identityLocked = student?.hasAccount === true

  function submit() {
    setProblem(undefined)
    const fields = { fullName, email, nisn, classroomId, dateOfBirth }
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }

    if (student) {
      update.mutate({ id: student.id, command: { ...fields, id: student.id, rowVersion: student.rowVersion } }, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    } else {
      create.mutate(fields, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    }
  }

  return (
    <FormDialog
      title={student ? t('students.form.editTitle') : t('students.form.newTitle')}
      problem={problem}
      errorPrefix="students.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      {identityLocked && <p className="rounded-md bg-muted p-3 text-sm">{t('common.managedByAccount')}</p>}

      <FormField label={t('students.form.fullName')} required error={fieldError(problem, 'fullName')}>
        <Input value={fullName} disabled={identityLocked} maxLength={200} onChange={(event) => setFullName(event.target.value)} />
      </FormField>

      <FormField label={t('students.form.email')} required hint={t('students.form.emailHint')} error={fieldError(problem, 'email')}>
        <Input type="email" value={email} disabled={identityLocked} maxLength={256} onChange={(event) => setEmail(event.target.value)} />
      </FormField>

      <FormField label={t('students.form.nisn')} required hint={t('students.form.nisnHint')} error={fieldError(problem, 'nisn')}>
        <Input value={nisn} inputMode="numeric" maxLength={10} onChange={(event) => setNisn(event.target.value)} />
      </FormField>

      <FormField
        label={t('students.form.classroom')}
        required
        hint={graduated ? t('students.form.graduatedNote') : undefined}
        error={fieldError(problem, 'classroomId')}
      >
        <Select value={classroomId} disabled={graduated} onChange={(event) => setClassroomId(event.target.value)}>
          <option value="">{t('students.form.classroomPlaceholder')}</option>
          {classrooms.data?.map((classroom) => (
            <option key={classroom.id} value={classroom.id}>
              {classroom.name}
            </option>
          ))}
          {/* A graduated student's last classroom belongs to an earlier year and is not in the list of the current one. */}
          {student && !classrooms.data?.some((c) => c.id === student.classroomId) && <option value={student.classroomId}>{student.classroomName}</option>}
        </Select>
      </FormField>

      <FormField label={t('students.form.dateOfBirth')} required error={fieldError(problem, 'dateOfBirth')}>
        <Input type="date" value={dateOfBirth} onChange={(event) => setDateOfBirth(event.target.value)} />
      </FormField>
    </FormDialog>
  )
}
