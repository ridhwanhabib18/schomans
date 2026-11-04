import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useClassroomStudents, useMyClassrooms } from '../api/progress'
import { NotesDialog } from '../components/NotesDialog'
import '../i18n'

function StudentList({ classroomId }: { classroomId: string }) {
  const { t } = useTranslation('progress')
  const students = useClassroomStudents(classroomId)
  const [open, setOpen] = useState<{ id: string; name: string } | null>(null)

  if (students.isPending) {
    return <Skeleton className="h-40 w-full" />
  }

  if (students.isError) {
    return <ErrorNotice message={t('notes.error')} onRetry={() => void students.refetch()} />
  }

  return (
    <>
      <ul className="flex flex-col divide-y divide-border rounded-md border border-border">
        {students.data.map((student) => (
          <li key={student.id} className="flex flex-wrap items-center gap-2 px-3 py-2 text-sm">
            <span className="font-medium">{student.fullName}</span>
            <Button variant="outline" size="sm" className="ml-auto" onClick={() => setOpen({ id: student.id, name: student.fullName })}>
              {t('review.actions.notes')}
              <span className="sr-only">: {student.fullName}</span>
            </Button>
          </li>
        ))}
      </ul>
      {open && <NotesDialog studentId={open.id} studentName={open.name} onClose={() => setOpen(null)} />}
    </>
  )
}

/** A teacher picks one of their classrooms and keeps short notes about its students. */
export function TeacherNotesPage() {
  const { t } = useTranslation('progress')
  const classrooms = useMyClassrooms()
  const [chosen, setChosen] = useState<string>()

  const classroomId = chosen && classrooms.data?.some((c) => c.id === chosen) ? chosen : (classrooms.data?.[0]?.id ?? null)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('notes.title')} description={t('notes.description')} />
      {classrooms.isPending ? (
        <Skeleton className="h-10 w-64" />
      ) : classrooms.isError ? (
        <ErrorNotice message={t('notes.error')} onRetry={() => void classrooms.refetch()} />
      ) : classroomId === null ? (
        <EmptyState title={t('notes.noClasses')} />
      ) : (
        <>
          <label className="flex max-w-sm flex-col gap-1.5 text-sm font-medium">
            {t('notes.pickClass')}
            <Select value={classroomId} onChange={(event) => setChosen(event.target.value)}>
              {classrooms.data.map((classroom) => (
                <option key={classroom.id} value={classroom.id}>
                  {classroom.name}
                </option>
              ))}
            </Select>
          </label>
          <StudentList key={classroomId} classroomId={classroomId} />
        </>
      )}
    </div>
  )
}
