import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import type { TodayLessonDto } from '@/api/generated/model'
import { parseProblem } from '@/shared/lib/problem'
import { formatTime } from '@/shared/lib/time'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useOpenSession, useTodayLessons } from '../api/attendance'
import '../i18n'

/** The teacher's lessons of today, each with the button to open (or go back to) its attendance session. */
export function TodayLessonsPage() {
  const { t } = useTranslation('attendance')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError, refetch } = useTodayLessons()
  const open = useOpenSession()

  function start(lesson: TodayLessonDto) {
    open.mutate(lesson.scheduleId, {
      onSuccess: (session) => void navigate(`/attendance/sessions/${session.id}`),
      onError: (error) => {
        const problem = parseProblem(error)
        showToast({ kind: 'error', title: t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
      },
    })
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('today.title')} description={t('today.description')} />

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-20" />
          <Skeleton className="h-20" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('today.error')} onRetry={() => void refetch()} />
      ) : data.length === 0 ? (
        <EmptyState title={t('today.empty.title')} description={t('today.empty.description')} />
      ) : (
        <ul className="flex flex-col gap-3">
          {data.map((lesson) => (
            <li key={lesson.scheduleId} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-card p-4">
              <div className="flex flex-col gap-1">
                <p className="font-mono text-sm text-muted-foreground">
                  {formatTime(lesson.startTime)}–{formatTime(lesson.endTime)}
                </p>
                <h2 className="font-semibold">
                  {lesson.subjectName}, {lesson.classroomName}
                </h2>
                <p className="text-sm">
                  {lesson.sessionId === null ? (
                    <Badge variant="muted">{t('today.notStarted')}</Badge>
                  ) : lesson.sessionOpen ? (
                    <Badge variant="success">{t('today.open', { present: lesson.presentCount, total: lesson.studentCount })}</Badge>
                  ) : (
                    <Badge variant="default">{t('today.closed', { present: lesson.presentCount, total: lesson.studentCount })}</Badge>
                  )}
                </p>
              </div>
              {lesson.sessionId === null ? (
                <Button disabled={open.isPending} onClick={() => start(lesson)}>
                  {t('today.start')}
                  <span className="sr-only">: {lesson.subjectName}, {lesson.classroomName}</span>
                </Button>
              ) : (
                <Link to={`/attendance/sessions/${lesson.sessionId}`} className={buttonVariants({ variant: lesson.sessionOpen ? 'default' : 'outline' })}>
                  {lesson.sessionOpen ? t('today.continue') : t('today.view')}
                  <span className="sr-only">: {lesson.subjectName}, {lesson.classroomName}</span>
                </Link>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
