import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ClassroomDto } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcademicYears, useCreateClassroom, useTeacherOptions, useUpdateClassroom } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

export function ClassroomFormDialog({ classroom, onClose }: { classroom?: ClassroomDto; onClose: () => void }) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateClassroom()
  const update = useUpdateClassroom()
  const years = useAcademicYears()
  const teachers = useTeacherOptions()
  const [name, setName] = useState(classroom?.name ?? '')
  const [academicYearId, setAcademicYearId] = useState(classroom?.academicYearId ?? '')
  const [homeroomTeacherId, setHomeroomTeacherId] = useState(classroom?.homeroomTeacherId ?? '')
  const [problem, setProblem] = useState<Problem>()

  // A new classroom starts in the current academic year unless the admin picks another.
  const currentYear = years.data?.items.find((year) => year.isActive)
  const yearValue = academicYearId || currentYear?.id || ''

  function submit() {
    setProblem(undefined)
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }

    if (classroom) {
      update.mutate(
        { id: classroom.id, command: { id: classroom.id, name, homeroomTeacherId, rowVersion: classroom.rowVersion } },
        { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) },
      )
    } else {
      create.mutate({ name, academicYearId: yearValue, homeroomTeacherId }, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    }
  }

  return (
    <FormDialog
      title={classroom ? t('classrooms.form.editTitle') : t('classrooms.form.newTitle')}
      problem={problem}
      errorPrefix="classrooms.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      <FormField label={t('classrooms.form.name')} required error={fieldError(problem, 'name')}>
        <Input value={name} maxLength={50} onChange={(event) => setName(event.target.value)} />
      </FormField>

      <FormField
        label={t('classrooms.form.year')}
        required
        hint={classroom ? t('classrooms.form.yearLocked') : undefined}
        error={fieldError(problem, 'academicYearId')}
      >
        <Select value={yearValue} disabled={classroom !== undefined} onChange={(event) => setAcademicYearId(event.target.value)}>
          <option value="">{t('classrooms.form.yearPlaceholder')}</option>
          {years.data?.items.map((year) => (
            <option key={year.id} value={year.id}>
              {year.name}
            </option>
          ))}
        </Select>
      </FormField>

      <FormField label={t('classrooms.form.homeroom')} required error={fieldError(problem, 'homeroomTeacherId')}>
        <Select value={homeroomTeacherId} onChange={(event) => setHomeroomTeacherId(event.target.value)}>
          <option value="">{t('classrooms.form.homeroomPlaceholder')}</option>
          {teachers.data?.map((teacher) => (
            <option key={teacher.id} value={teacher.id}>
              {teacher.fullName}
            </option>
          ))}
        </Select>
      </FormField>
    </FormDialog>
  )
}
