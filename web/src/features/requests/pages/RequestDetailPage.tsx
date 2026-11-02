import { useId, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { formatDateTime } from '@/shared/lib/format'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCancelRequest, useDecideRequest, useOpenLetter, useRequest } from '../api/requests'
import type { RequestDetail } from '../api/types'
import { ChainSteps, DaysText, StatusBadge } from '../components/RequestParts'
import { requestErrorText } from '../errors'
import '../i18n'

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 break-words font-medium">{children}</dd>
    </div>
  )
}

type Asking = 'approve' | 'reject' | 'withdraw' | null

function Actions({ request }: { request: RequestDetail }) {
  const { t } = useTranslation('requests')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const decide = useDecideRequest(request.id)
  const cancel = useCancelRequest(request.id)
  const [asking, setAsking] = useState<Asking>(null)
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState<string>()
  const [error, setError] = useState<string>()
  const noteId = useId()

  const close = () => {
    setAsking(null)
    setNote('')
    setNoteError(undefined)
  }

  function confirmDecision(approve: boolean) {
    if (!approve && note.trim() === '') {
      setNoteError(t('detail.decide.noteNeeded'))
      return
    }

    setError(undefined)
    decide.mutate(
      { approve, note: note.trim() === '' ? null : note.trim() },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('detail.decide.done') })
          close()
        },
        onError: (e) => {
          setError(requestErrorText(e, t, tCommon('errors.generic')))
          close()
        },
      },
    )
  }

  function confirmWithdraw() {
    setError(undefined)
    cancel.mutate(undefined, {
      onSuccess: () => {
        showToast({ kind: 'success', title: t('detail.cancel.done') })
        close()
      },
      onError: (e) => {
        setError(requestErrorText(e, t, tCommon('errors.generic')))
        close()
      },
    })
  }

  const noteField = (label: string) => (
    <span className="mt-2 flex flex-col gap-1.5 text-sm">
      <label htmlFor={noteId} className="font-medium text-foreground">
        {label}
      </label>
      <Textarea id={noteId} rows={3} maxLength={500} value={note} aria-invalid={noteError ? true : undefined} onChange={(event) => setNote(event.target.value)} />
      {noteError && (
        <span role="alert" className="text-xs text-destructive">
          {noteError}
        </span>
      )}
    </span>
  )

  return (
    <div className="flex flex-col gap-3">
      {error && <Alert tone="error" message={error} />}
      {request.canDecide && (
        <>
          <p className="text-sm">{t('detail.yourTurn')}</p>
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => setAsking('approve')}>{t('detail.approve')}</Button>
            <Button variant="outline" onClick={() => setAsking('reject')}>
              {t('detail.reject')}
            </Button>
          </div>
        </>
      )}
      {request.canCancel && (
        <div>
          <Button variant="outline" onClick={() => setAsking('withdraw')}>
            {t('detail.withdraw')}
          </Button>
        </div>
      )}
      <ConfirmDialog
        open={asking === 'approve'}
        onOpenChange={(open) => !open && close()}
        title={t('detail.decide.approveTitle')}
        description={
          <>
            {t('detail.decide.approveBody')}
            {noteField(t('detail.decide.noteOptional'))}
          </>
        }
        confirmLabel={t('detail.decide.approveConfirm')}
        pending={decide.isPending}
        onConfirm={() => confirmDecision(true)}
      />
      <ConfirmDialog
        open={asking === 'reject'}
        onOpenChange={(open) => !open && close()}
        title={t('detail.decide.rejectTitle')}
        description={
          <>
            {t('detail.decide.rejectBody')}
            {noteField(t('detail.decide.note'))}
          </>
        }
        confirmLabel={t('detail.decide.rejectConfirm')}
        destructive
        pending={decide.isPending}
        onConfirm={() => confirmDecision(false)}
      />
      <ConfirmDialog
        open={asking === 'withdraw'}
        onOpenChange={(open) => !open && close()}
        title={t('detail.cancel.title')}
        description={t('detail.cancel.body')}
        confirmLabel={t('detail.cancel.confirm')}
        destructive
        pending={cancel.isPending}
        onConfirm={confirmWithdraw}
      />
    </div>
  )
}

function LetterSection({ request }: { request: RequestDetail }) {
  const { t } = useTranslation('requests')
  const open = useOpenLetter()

  if (!request.letter) {
    return null
  }

  return (
    <Card className="min-w-0">
      <CardHeader>
        <CardTitle>{t('detail.letter')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col items-start gap-3">
        <p>{t('detail.letterNumber', { number: request.letter.letterNumber })}</p>
        {open.isError && <Alert tone="error" message={requestErrorText(open.error, t, t('detail.downloadError'))} />}
        <Button disabled={open.isPending} onClick={() => open.mutate(request.id)}>
          {open.isPending ? t('detail.opening') : t('detail.download')}
        </Button>
      </CardContent>
    </Card>
  )
}

/** A request with its chain, the decisions so far, and the actions the signed-in user may take. */
export function RequestDetailPage() {
  const { t } = useTranslation('requests')
  const { id = '' } = useParams()
  const { data, isPending, isError, error, refetch } = useRequest(id)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <Link to="/requests" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'self-start' })}>
        {t('detail.back')}
      </Link>
      {isPending ? (
        <Skeleton className="h-64" />
      ) : isError ? (
        <ErrorNotice message={requestErrorText(error, t, t('detail.error'))} onRetry={() => void refetch()} />
      ) : (
        <>
          <header className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight">{t(`type.${data.type}`)}</h1>
            <StatusBadge status={data.status} />
          </header>

          <Card className="min-w-0">
            <CardContent className="pt-6">
              <dl className="grid gap-4 sm:grid-cols-2 [&>*]:min-w-0">
                <Fact label={t('detail.requester')}>{data.requesterName}</Fact>
                {data.studentName && <Fact label={t('detail.student')}>{data.studentName}</Fact>}
                {data.classroomName && <Fact label={t('detail.class')}>{data.classroomName}</Fact>}
                {data.startDate && (
                  <Fact label={t('detail.days')}>
                    <DaysText from={data.startDate} to={data.endDate} />
                  </Fact>
                )}
                <Fact label={t('detail.made')}>{formatDateTime(data.createdAt)}</Fact>
                <div className="min-w-0 sm:col-span-2">
                  <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t('detail.reason')}</dt>
                  <dd className="mt-0.5 whitespace-pre-wrap break-words">{data.reason}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          <section aria-labelledby="chain-title" className="flex min-w-0 flex-col gap-3">
            <h2 id="chain-title" className="text-lg font-semibold">
              {t('detail.chain')}
            </h2>
            <ChainSteps chain={data.chain} currentStep={data.currentStep} closed={data.status !== 'Pending'} />
          </section>

          <section aria-labelledby="decisions-title" className="flex min-w-0 flex-col gap-3">
            <h2 id="decisions-title" className="text-lg font-semibold">
              {t('detail.decisions')}
            </h2>
            {data.approvals.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('detail.noDecisions')}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {data.approvals.map((approval) => (
                  <li key={approval.step} className="flex min-w-0 flex-col gap-1 rounded-lg border border-border p-3 text-sm">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={approval.decision === 'Approved' ? 'success' : 'destructive'}>{t(`decision.${approval.decision}`)}</Badge>
                      <span className="font-medium">{t('detail.decisionLine', { role: t(`role.${approval.role}`), name: approval.approverName })}</span>
                      <span className="text-muted-foreground">{formatDateTime(approval.decidedAt)}</span>
                    </div>
                    {approval.notes && <p className="whitespace-pre-wrap break-words text-muted-foreground">{approval.notes}</p>}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <LetterSection request={data} />
          <Actions request={data} />
        </>
      )}
    </div>
  )
}
