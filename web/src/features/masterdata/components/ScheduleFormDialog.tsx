import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DayOfWeek, ScheduleDto } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { formatTime, toApiTime, WEEK_DAYS } from '@/shared/lib/time'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateSchedule, useSubjects, useTeacherOptions, useUpdateSchedule } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

interface ScheduleFormDialogProps {
  /** The classroom of a new lesson. A saved lesson keeps its classroom. */
  classroomId: string
  classroomName: string
  lesson?: ScheduleDto
  onClose: () => void
}

export function ScheduleFormDialog({ classroomId, classroomName, lesson, onClose }: ScheduleFormDialogProps) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateSchedule()
  const update = useUpdateSchedule()
  const subjects = useSubjects({ Page: 1, PageSize: 100, Sort: 'name' })
  const teachers = useTeacherOptions()
  const [day, setDay] = useState<string>(lesson?.dayOfWeek ?? 'Monday')
  const [start, setStart] = useState(lesson ? formatTime(lesson.startTime) : '07:00')
  const [end, setEnd] = useState(lesson ? formatTime(lesson.endTime) : '08:30')
  const [subjectId, setSubjectId] = useState(lesson?.subjectId ?? '')
  const [teacherId, setTeacherId] = useState(lesson?.teacherId ?? '')
  const [problem, setProblem] = useState<Problem>()

  function submit() {
    setProblem(undefined)
    const fields = { subjectId, teacherId, dayOfWeek: day as DayOfWeek, startTime: toApiTime(start), endTime: toApiTime(end) }
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }
    const failed = (error: unknown) => setProblem(parseProblem(error))

    if (lesson) {
      update.mutate({ id: lesson.id, command: { ...fields, id: lesson.id, rowVersion: lesson.rowVersion } }, { onSuccess: done, onError: failed })
    } else {
      create.mutate({ ...fields, classroomId }, { onSuccess: done, onError: failed })
    }
  }

  return (
    <FormDialog
      title={lesson ? t('schedules.form.editTitle') : t('schedules.form.newTitle')}
      description={t('schedules.form.classroom', { name: classroomName })}
      problem={problem}
      errorPrefix="schedules.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      <FormField label={t('schedules.form.day')} required error={fieldError(problem, 'dayOfWeek')}>
        <Select value={day} onChange={(event) => setDay(event.target.value)}>
          {WEEK_DAYS.map((value) => (
            <option key={value} value={value}>
              {tCommon(`days.${value}`)}
            </option>
          ))}
        </Select>
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label={t('schedules.form.start')} required error={fieldError(problem, 'startTime')}>
          <Input type="time" value={start} onChange={(event) => setStart(event.target.value)} />
        </FormField>
        <FormField label={t('schedules.form.end')} required error={fieldError(problem, 'endTime')}>
          <Input type="time" value={end} onChange={(event) => setEnd(event.target.value)} />
        </FormField>
      </div>

      <FormField label={t('schedules.form.subject')} required error={fieldError(problem, 'subjectId')}>
        <Select value={subjectId} onChange={(event) => setSubjectId(event.target.value)}>
          <option value="">{t('schedules.form.subjectPlaceholder')}</option>
          {subjects.data?.items.map((subject) => (
            <option key={subject.id} value={subject.id}>
              {subject.name}
            </option>
          ))}
        </Select>
      </FormField>

      <FormField label={t('schedules.form.teacher')} required error={fieldError(problem, 'teacherId')}>
        <Select value={teacherId} onChange={(event) => setTeacherId(event.target.value)}>
          <option value="">{t('schedules.form.teacherPlaceholder')}</option>
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
