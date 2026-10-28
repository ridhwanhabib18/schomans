import { ArrowLeft, Maximize2, Minimize2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import type { SessionStudentDto } from '@/api/generated/model'
import { formatDate, formatDateTime } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { formatTime } from '@/shared/lib/time'
import { cn } from '@/shared/lib/utils'
import { Alert } from '@/shared/ui/alert'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCloseSession, useLiveSession, useSession } from '../api/attendance'
import { MarkStudentDialog } from '../components/MarkStudentDialog'
import { QrPanel } from '../components/QrPanel'
import { StatusBadge } from '../components/StatusBadge'
import { selectPresenting, selectSetPresenting, useAttendanceSessionStore } from '../store'
import '../i18n'

/**
 * The teacher's screen of a session: the rotating QR code for the projector and the class list, updated live as
 * students scan. The teacher can set a status by hand and close the session. Admin staff and the principal see the
 * list without the code.
 */
export function SessionPage() {
  const { t } = useTranslation('attendance')
  const { t: tCommon } = useTranslation()
  const { id = '' } = useParams()
  const isTeacher = useAuthStore(selectAuthRole) === 'Teacher'
  const showToast = useToastStore(selectShowToast)
  const presenting = useAttendanceSessionStore(selectPresenting)
  const setPresenting = useAttendanceSessionStore(selectSetPresenting)
  const { data, isPending, isError, refetch } = useSession(id)
  const live = useLiveSession(id, data !== undefined)
  const close = useCloseSession()
  const [confirmClose, setConfirmClose] = useState(false)
  const [marking, setMarking] = useState<SessionStudentDto | null>(null)

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="flex flex-col gap-4">
        <Skeleton className="h-10 w-2/3" />
        <Skeleton className="h-80" />
      </div>
    )
  }

  if (isError) {
    return <ErrorNotice message={t('session.error')} onRetry={() => void refetch()} />
  }

  // The API only shows a teacher their own sessions, so a teacher here is the teacher of the lesson.
  const canChange = isTeacher
  const present = data.students.filter((s) => s.status === 'Present').length
  const waiting = data.students.filter((s) => s.status === null).length

  if (presenting && data.qr) {
    return (
      <div className="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4 bg-background p-6">
        <p className="text-xl font-semibold">{t('session.title', { subject: data.subjectName, classroom: data.classroomName })}</p>
        <QrPanel qr={data.qr} large />
        <p className="text-lg tabular-nums">{t('session.counts', { present, waiting })}</p>
        <Button variant="outline" onClick={() => setPresenting(false)}>
          <Minimize2 aria-hidden="true" />
          {t('session.exitPresent')}
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      {isTeacher && (
        <Link to="/attendance" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
          <ArrowLeft aria-hidden="true" />
          {t('session.back')}
        </Link>
      )}
      <PageHeader
        title={t('session.title', { subject: data.subjectName, classroom: data.classroomName })}
        description={t('session.subtitle', { date: formatDate(data.date), start: formatTime(data.startTime), end: formatTime(data.endTime) })}
        actions={
          <span className={cn('flex items-center gap-2 text-sm', live === 'live' ? 'text-emerald-600' : 'text-muted-foreground')}>
            <span aria-hidden="true" className={cn('size-2 rounded-full', live === 'live' ? 'bg-emerald-500' : 'bg-muted-foreground')} />
            {t(`session.${live}`)}
          </span>
        }
      />

      {!isTeacher && <Alert tone="info" message={t('session.readOnly')} />}
      {!data.isOpen && <Alert tone="info" message={t('session.closedNote')} />}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,26rem)_1fr]">
        {data.qr && (
          <section className="flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-4">
            <QrPanel qr={data.qr} />
            <div className="flex flex-wrap justify-center gap-2">
              <Button variant="outline" onClick={() => setPresenting(true)}>
                <Maximize2 aria-hidden="true" />
                {t('session.present')}
              </Button>
              <Button variant="outline" className="text-destructive" onClick={() => setConfirmClose(true)}>
                {t('session.close')}
              </Button>
            </div>
          </section>
        )}

        <section aria-labelledby="session-students" className="flex flex-col gap-3">
          <h2 id="session-students" className="font-semibold">
            {t('session.listLabel')}
          </h2>
          <p className="text-sm text-muted-foreground" aria-live="polite">
            {t('session.counts', { present, waiting })}
          </p>
          <ul className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card">
            {data.students.map((student) => (
              <li key={student.studentId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                <div className="min-w-0">
                  <p className="font-medium">{student.fullName}</p>
                  <p className="text-xs text-muted-foreground">
                    {student.nisn}
                    {student.scannedAt && ` · ${t('session.scannedAt', { time: formatDateTime(student.scannedAt) })}`}
                    {student.isManual && ` · ${t('session.manual')}`}
                    {student.note && ` · ${student.note}`}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <StatusBadge status={student.status} />
                  {canChange && (
                    <Button variant="ghost" size="sm" onClick={() => setMarking(student)}>
                      {t('session.change')}
                      <span className="sr-only">: {t('session.changeFor', { name: student.fullName })}</span>
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </section>
      </div>

      {marking && <MarkStudentDialog sessionId={data.id} student={marking} onClose={() => setMarking(null)} />}

      <ConfirmDialog
        open={confirmClose}
        onOpenChange={setConfirmClose}
        title={t('session.closeTitle')}
        description={t('session.closeBody', { count: waiting })}
        confirmLabel={t('session.close')}
        destructive
        pending={close.isPending}
        onConfirm={() =>
          close.mutate(data.id, {
            onSuccess: () => {
              setConfirmClose(false)
              showToast({ kind: 'success', title: t('session.closed') })
            },
            onError: (error) => {
              setConfirmClose(false)
              const problem = parseProblem(error)
              showToast({ kind: 'error', title: t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
            },
          })
        }
      />
    </div>
  )
}
