import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ScheduleDto } from '@/api/generated/model'
import { parseProblem } from '@/shared/lib/problem'
import { formatTime, WEEK_DAYS } from '@/shared/lib/time'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useActiveClassrooms, useDeleteSchedule, useSchedules } from '../api/masterdata'
import { ScheduleFormDialog } from '../components/ScheduleFormDialog'
import '../i18n'

/** The weekly timetable of one classroom, day by day. Staff read it; admin staff change it. */
export function SchedulesPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const canEdit = useAuthStore(selectAuthRole) === 'Admin'
  const showToast = useToastStore(selectShowToast)
  const classrooms = useActiveClassrooms()
  const [chosen, setChosen] = useState('')
  const classroomId = chosen || classrooms.data?.[0]?.id || ''
  const classroom = classrooms.data?.find((c) => c.id === classroomId)
  const { data, isPending, isError, refetch } = useSchedules({ ClassroomId: classroomId, Page: 1, PageSize: 100 }, classroomId !== '')
  const [editing, setEditing] = useState<ScheduleDto | 'new' | null>(null)
  const [deleting, setDeleting] = useState<ScheduleDto | null>(null)
  const remove = useDeleteSchedule()

  // Monday to Friday always; the weekend only when it has lessons.
  const days = WEEK_DAYS.filter((day, index) => index < 5 || data?.items.some((lesson) => lesson.dayOfWeek === day))

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('schedules.title')}
        description={t('schedules.description')}
        actions={
          canEdit &&
          classroom && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden="true" />
              {t('schedules.add')}
            </Button>
          )
        }
      />

      <label className="flex max-w-xs flex-col gap-1.5 text-sm font-medium">
        {t('schedules.classroom')}
        <Select value={classroomId} onChange={(event) => setChosen(event.target.value)}>
          {classrooms.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
      </label>

      {isError || classrooms.isError ? (
        <ErrorNotice message={t('common.error')} onRetry={() => void refetch()} />
      ) : classrooms.isPending || (classroomId !== '' && isPending) ? (
        <div role="status" aria-busy="true" className="grid gap-3 md:grid-cols-5">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-48" />
          ))}
        </div>
      ) : !classroom ? (
        <EmptyState title={t('schedules.noClassrooms')} />
      ) : (
        <div className="grid gap-3 md:grid-cols-[repeat(auto-fit,minmax(10rem,1fr))]">
          {days.map((day) => {
            const lessons = data?.items.filter((lesson) => lesson.dayOfWeek === day) ?? []
            return (
              <section key={day} aria-labelledby={`day-${day}`} className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3">
                <h2 id={`day-${day}`} className="text-sm font-semibold">
                  {tCommon(`days.${day}`)}
                </h2>
                {lessons.length === 0 ? (
                  <p className="text-xs text-muted-foreground">{t('schedules.free')}</p>
                ) : (
                  <ul className="flex flex-col gap-2">
                    {lessons.map((lesson) => (
                      <li key={lesson.id} className="rounded-md bg-muted/60 p-2 text-sm">
                        <p className="font-mono text-xs text-muted-foreground">
                          {formatTime(lesson.startTime)}–{formatTime(lesson.endTime)}
                        </p>
                        <p className="font-medium">{lesson.subjectName}</p>
                        <p className="text-xs text-muted-foreground">{lesson.teacherName}</p>
                        {canEdit && (
                          <div className="mt-1 flex gap-1">
                            <Button variant="ghost" size="sm" className="h-7 px-2" onClick={() => setEditing(lesson)}>
                              <Pencil aria-hidden="true" />
                              <span className="sr-only">
                                {tCommon('actions.edit')}: {lesson.subjectName} {tCommon(`days.${day}`)} {formatTime(lesson.startTime)}
                              </span>
                            </Button>
                            <Button variant="ghost" size="sm" className="h-7 px-2" onClick={() => setDeleting(lesson)}>
                              <Trash2 aria-hidden="true" />
                              <span className="sr-only">
                                {tCommon('actions.delete')}: {lesson.subjectName} {tCommon(`days.${day}`)} {formatTime(lesson.startTime)}
                              </span>
                            </Button>
                          </div>
                        )}
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            )
          })}
        </div>
      )}

      {editing !== null && classroom && (
        <ScheduleFormDialog
          classroomId={classroom.id}
          classroomName={classroom.name}
          lesson={editing === 'new' ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      )}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('schedules.delete.title')}
        description={t('schedules.delete.body', {
          subject: deleting?.subjectName,
          day: deleting ? tCommon(`days.${deleting.dayOfWeek}`) : '',
          time: deleting ? formatTime(deleting.startTime) : '',
        })}
        confirmLabel={t('schedules.delete.confirm')}
        destructive
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(deleting!.id, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('common.deleted') })
              setDeleting(null)
            },
            onError: (error) => {
              const problem = parseProblem(error)
              showToast({ kind: 'error', title: t(`schedules.errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
              setDeleting(null)
            },
          })
        }
      />
    </div>
  )
}
