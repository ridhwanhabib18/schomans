import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formatDate, formatDateTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/badge'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { cn } from '@/shared/lib/utils'
import type { ReactNode } from 'react'
import type { ApproverRole, RequestListItem, RequestStatus } from '../api/types'
import '../i18n'

const STATUS_VARIANT: Record<RequestStatus, 'warning' | 'success' | 'destructive' | 'muted'> = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'destructive',
  Cancelled: 'muted',
}

export function StatusBadge({ status }: { status: RequestStatus }) {
  const { t } = useTranslation('requests')
  return <Badge variant={STATUS_VARIANT[status]}>{t(`status.${status}`)}</Badge>
}

/** The first and last day of a leave; a letter has no days. */
export function DaysText({ from, to }: { from: string | null; to: string | null }) {
  const { t } = useTranslation('requests')
  return <>{from && to ? t('days', { from: formatDate(from), to: formatDate(to) }) : t('noDays')}</>
}

/** The list of requests of every page that lists them: the same columns, a link to the request on the kind. */
export function RequestTable({
  rows,
  isLoading,
  caption,
  empty,
  showRequester = true,
}: {
  rows: RequestListItem[] | undefined
  isLoading: boolean
  caption: string
  empty: ReactNode
  showRequester?: boolean
}) {
  const { t } = useTranslation('requests')

  const columns: Column<RequestListItem>[] = [
    {
      key: 'kind',
      header: t('columns.kind'),
      render: (r) => (
        <Link to={`/requests/${r.id}`} className="font-medium text-primary hover:underline">
          {t(`type.${r.type}`)}
        </Link>
      ),
    },
    ...(showRequester ? [{ key: 'requester', header: t('columns.requester'), render: (r: RequestListItem) => r.requesterName }] : []),
    { key: 'student', header: t('columns.student'), render: (r) => r.studentName ?? t('noStudent') },
    { key: 'days', header: t('columns.days'), className: 'tabular-nums', render: (r) => <DaysText from={r.startDate} to={r.endDate} /> },
    {
      key: 'status',
      header: t('columns.status'),
      render: (r) => (
        <div className="flex flex-col items-start gap-1">
          <StatusBadge status={r.status} />
          {r.status === 'Pending' && r.waitingFor && (
            <span className="text-xs text-muted-foreground">{t('waiting', { step: r.currentStep, total: r.totalSteps, role: t(`role.${r.waitingFor}`) })}</span>
          )}
        </div>
      ),
    },
    { key: 'made', header: t('columns.made'), className: 'tabular-nums', render: (r) => formatDateTime(r.createdAt) },
  ]

  return <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} isLoading={isLoading} empty={empty} caption={caption} />
}

/** The chain as numbered steps: the ones decided are marked, the one that waits now is highlighted. */
export function ChainSteps({ chain, currentStep, closed }: { chain: ApproverRole[]; currentStep: number; closed: boolean }) {
  const { t } = useTranslation('requests')

  return (
    <ol className="flex flex-wrap gap-3 [&>li]:min-w-0">
      {chain.map((role, index) => {
        const step = index + 1
        const state = closed || step < currentStep ? 'done' : step === currentStep ? 'current' : 'next'
        return (
          <li
            key={step}
            aria-current={state === 'current' ? 'step' : undefined}
            className={cn(
              'flex min-w-0 flex-col rounded-lg border px-3 py-2 text-sm',
              state === 'current' && 'border-primary bg-primary/5',
              state === 'done' && 'border-border bg-muted',
              state === 'next' && 'border-dashed border-border',
            )}
          >
            <span className="font-medium">{t('detail.chainStep', { step, role: t(`role.${role}`) })}</span>
            {state === 'current' && <span className="text-xs text-primary">{t('detail.current')}</span>}
            {state === 'done' && <span className="text-xs text-muted-foreground">{t('detail.done')}</span>}
          </li>
        )
      })}
    </ol>
  )
}
