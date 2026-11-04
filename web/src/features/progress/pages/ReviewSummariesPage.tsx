import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { currentMonth, shiftMonth } from '@/shared/lib/time'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Card, CardContent } from '@/shared/ui/card'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Select } from '@/shared/ui/form-controls'
import { MonthPicker } from '@/shared/ui/month-picker'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import {
  useApproveSummary,
  useClassroomStudents,
  useClassroomSummaries,
  useMyClassrooms,
  useRegenerateSummary,
  useSendSummaries,
} from '../api/progress'
import type { ProgressSummary, SummaryStatus } from '../api/types'
import { EditSummaryDialog } from '../components/EditSummaryDialog'
import { NotesDialog } from '../components/NotesDialog'
import { SummaryBody } from '../components/SummaryBody'
import { progressErrorText } from '../errors'
import '../i18n'

const STATUS_VARIANT: Record<SummaryStatus, 'warning' | 'default' | 'success'> = { Draft: 'warning', Approved: 'default', Sent: 'success' }

function SummaryCard({ summary, month, onEdit, onNotes }: { summary: ProgressSummary; month: string; onEdit: () => void; onNotes: () => void }) {
  const { t } = useTranslation('progress')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const approve = useApproveSummary()
  const regenerate = useRegenerateSummary()
  const [error, setError] = useState<string>()
  const locked = summary.status === 'Sent'
  const fail = (e: unknown) => setError(progressErrorText(e, t, tCommon('errors.generic')))

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="font-semibold">{summary.studentName}</h3>
          <Badge variant={STATUS_VARIANT[summary.status]}>{t(`review.status.${summary.status}`)}</Badge>
          <Badge variant="muted">{summary.isAiDrafted ? t('review.aiDraft') : t('review.edited')}</Badge>
        </div>
        <SummaryBody body={summary.body} />
        {error && (
          <p role="alert" className="text-sm font-medium text-destructive">
            {error}
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" disabled={locked} onClick={onEdit}>
            {t('review.actions.edit')}
            <span className="sr-only">: {summary.studentName}</span>
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={locked || regenerate.isPending}
            onClick={() => {
              setError(undefined)
              regenerate.mutate(
                { studentId: summary.studentId, month },
                { onSuccess: () => showToast({ kind: 'success', title: t('review.regenerated') }), onError: fail },
              )
            }}
          >
            {t('review.actions.regenerate')}
            <span className="sr-only">: {summary.studentName}</span>
          </Button>
          <Button variant="ghost" size="sm" onClick={onNotes}>
            {t('review.actions.notes')}
            <span className="sr-only">: {summary.studentName}</span>
          </Button>
          <Button
            size="sm"
            className="ml-auto"
            disabled={summary.status !== 'Draft' || approve.isPending}
            onClick={() => {
              setError(undefined)
              approve.mutate(summary.id, { onSuccess: () => showToast({ kind: 'success', title: t('review.approved') }), onError: fail })
            }}
          >
            {t('review.actions.approve')}
            <span className="sr-only">: {summary.studentName}</span>
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

function ClassroomReview({ classroomId }: { classroomId: string }) {
  const { t } = useTranslation('progress')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const [month, setMonth] = useState(() => shiftMonth(currentMonth(), -1))
  const summaries = useClassroomSummaries(classroomId, month)
  const students = useClassroomStudents(classroomId)
  const regenerate = useRegenerateSummary()
  const send = useSendSummaries()
  const [editing, setEditing] = useState<ProgressSummary | null>(null)
  const [notesFor, setNotesFor] = useState<{ id: string; name: string } | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string>()

  const approvedCount = summaries.data?.filter((s) => s.status === 'Approved').length ?? 0
  const sentCount = summaries.data?.filter((s) => s.status === 'Sent').length ?? 0
  const withSummary = new Set(summaries.data?.map((s) => s.studentId))
  const missing = students.data?.filter((s) => !withSummary.has(s.id)) ?? []

  function sendApproved() {
    setError(undefined)
    send.mutate(
      { classroomId, month },
      {
        onSuccess: (result) => {
          setConfirming(false)
          showToast({ kind: 'success', title: t('review.sent', { sent: result.sent, parents: result.parentsNotified }) })
        },
        onError: (e) => setError(progressErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <section className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <MonthPicker month={month} onChange={setMonth} />
        {summaries.data && summaries.data.length > 0 && (
          <span className="text-sm text-muted-foreground">
            {t('review.summary', { count: summaries.data.length, approved: approvedCount, sent: sentCount })}
          </span>
        )}
        <Button className="ml-auto" disabled={approvedCount === 0} onClick={() => setConfirming(true)}>
          {t('review.send', { count: approvedCount })}
        </Button>
      </div>

      {summaries.isPending ? (
        <Skeleton className="h-48 w-full" />
      ) : summaries.isError ? (
        <ErrorNotice message={progressErrorText(summaries.error, t, t('review.error'))} onRetry={() => void summaries.refetch()} />
      ) : summaries.data.length === 0 ? (
        <EmptyState title={t('review.empty')} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {summaries.data.map((summary) => (
            <SummaryCard
              key={summary.id}
              summary={summary}
              month={month}
              onEdit={() => setEditing(summary)}
              onNotes={() => setNotesFor({ id: summary.studentId, name: summary.studentName })}
            />
          ))}
        </div>
      )}

      {missing.length > 0 && (
        <section className="flex flex-col gap-2">
          <h2 className="text-base font-semibold">{t('review.missing.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('review.missing.hint')}</p>
          <ul className="flex flex-col divide-y divide-border rounded-md border border-border">
            {missing.map((student) => (
              <li key={student.id} className="flex flex-wrap items-center gap-2 px-3 py-2 text-sm">
                <span className="font-medium">{student.fullName}</span>
                <Button variant="ghost" size="sm" className="ml-auto" onClick={() => setNotesFor({ id: student.id, name: student.fullName })}>
                  {t('review.actions.notes')}
                  <span className="sr-only">: {student.fullName}</span>
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={regenerate.isPending}
                  onClick={() => {
                    setError(undefined)
                    regenerate.mutate(
                      { studentId: student.id, month },
                      {
                        onSuccess: () => showToast({ kind: 'success', title: t('review.regenerated') }),
                        onError: (e) => showToast({ kind: 'error', title: progressErrorText(e, t, tCommon('errors.generic')) }),
                      },
                    )
                  }}
                >
                  {t('review.missing.draft')}
                  <span className="sr-only">: {student.fullName}</span>
                </Button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {editing && <EditSummaryDialog summary={editing} onClose={() => setEditing(null)} />}
      {notesFor && <NotesDialog studentId={notesFor.id} studentName={notesFor.name} onClose={() => setNotesFor(null)} />}
      <ConfirmDialog
        open={confirming}
        onOpenChange={(open) => {
          if (!open) {
            setConfirming(false)
            setError(undefined)
          }
        }}
        title={t('review.sendTitle')}
        description={
          <span className="flex flex-col gap-3">
            <span>{t('review.sendBody', { count: approvedCount })}</span>
            {error && (
              <span role="alert" className="font-medium text-destructive">
                {error}
              </span>
            )}
          </span>
        }
        confirmLabel={t('review.sendConfirm')}
        pending={send.isPending}
        onConfirm={sendApproved}
      />
    </section>
  )
}

/** The homeroom teacher reviews the monthly summaries of the class: edit, approve, ask for a new draft, then send in bulk. */
export function ReviewSummariesPage() {
  const { t } = useTranslation('progress')
  const classrooms = useMyClassrooms()
  const [chosen, setChosen] = useState<string>()

  const homeroom = classrooms.data?.filter((c) => c.isHomeroom) ?? []
  const classroomId = chosen && homeroom.some((c) => c.id === chosen) ? chosen : (homeroom[0]?.id ?? null)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('review.title')} description={t('review.description')} />
      {classrooms.isPending ? (
        <Skeleton className="h-10 w-64" />
      ) : classrooms.isError ? (
        <ErrorNotice message={t('review.error')} onRetry={() => void classrooms.refetch()} />
      ) : classroomId === null ? (
        <EmptyState title={t('review.noHomeroom')} />
      ) : (
        <>
          {homeroom.length > 1 && (
            <label className="flex max-w-sm flex-col gap-1.5 text-sm font-medium">
              {t('review.pickClass')}
              <Select value={classroomId} onChange={(event) => setChosen(event.target.value)}>
                {homeroom.map((classroom) => (
                  <option key={classroom.id} value={classroom.id}>
                    {classroom.name}
                  </option>
                ))}
              </Select>
            </label>
          )}
          <ClassroomReview key={classroomId} classroomId={classroomId} />
        </>
      )}
    </div>
  )
}
