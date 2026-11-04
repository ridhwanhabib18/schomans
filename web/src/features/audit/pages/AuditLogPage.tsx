import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/badge'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { PAGE_SIZE, useAuditLogs } from '../api/audit'
import { AUDIT_ENTITIES, type AuditAction, type AuditEntity, type AuditFilters, type AuditLog } from '../api/types'
import '../i18n'

const ACTION_VARIANT: Record<AuditAction, 'success' | 'warning' | 'destructive'> = {
  Created: 'success',
  Updated: 'warning',
  Deleted: 'destructive',
}

function parse(json: string | null): Record<string, unknown> {
  if (!json) {
    return {}
  }
  try {
    const value: unknown = JSON.parse(json)
    return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  } catch {
    return {}
  }
}

const show = (value: unknown) => (value === null || value === undefined || value === '' ? '–' : String(value))

/** What changed, one line per field: the old and the new value of an update, the values of a new or deleted row. */
function Changes({ log }: { log: AuditLog }) {
  const { t } = useTranslation('audit')
  const before = parse(log.oldValues)
  const after = parse(log.newValues)
  const fields = Object.keys(log.action === 'Deleted' ? before : after)

  if (fields.length === 0) {
    return <span className="text-muted-foreground">{t('noChanges')}</span>
  }

  return (
    <ul className="flex min-w-0 flex-col gap-0.5 text-sm">
      {fields.map((field) => (
        <li key={field} className="break-words">
          {log.action === 'Updated'
            ? t('change', { field, from: show(before[field]), to: show(after[field]) })
            : t('value', { field, value: show(log.action === 'Deleted' ? before[field] : after[field]) })}
        </li>
      ))}
    </ul>
  )
}

/** The audit trail for admin staff and the principal (F-11): read-only, newest first, filtered by record kind and day. */
export function AuditLogPage() {
  const { t } = useTranslation('audit')
  const [filters, setFilters] = useState<AuditFilters>({ entityName: undefined, from: undefined, to: undefined })
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useAuditLogs(filters, page)

  const change = (next: Partial<AuditFilters>) => {
    setFilters((current) => ({ ...current, ...next }))
    setPage(1)
  }

  const columns: Column<AuditLog>[] = [
    { key: 'when', header: t('columns.when'), className: 'whitespace-nowrap tabular-nums', render: (l) => formatDateTime(l.at) },
    { key: 'who', header: t('columns.who'), render: (l) => l.userName ?? <span className="text-muted-foreground">{t('system')}</span> },
    { key: 'action', header: t('columns.action'), render: (l) => <Badge variant={ACTION_VARIANT[l.action]}>{t(`action.${l.action}`)}</Badge> },
    { key: 'record', header: t('columns.record'), render: (l) => t(`entity.${l.entityName}`, { defaultValue: l.entityName }) },
    { key: 'changes', header: t('columns.changes'), render: (l) => <Changes log={l} /> },
  ]

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('title')} description={t('description')} />
      <div className="flex flex-wrap gap-4">
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
          {t('filters.entity')}
          <Select value={filters.entityName ?? ''} onChange={(event) => change({ entityName: (event.target.value || undefined) as AuditEntity | undefined })}>
            <option value="">{t('filters.allEntities')}</option>
            {AUDIT_ENTITIES.map((entity) => (
              <option key={entity} value={entity}>
                {t(`entity.${entity}`)}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
          {t('filters.from')}
          <Input type="date" value={filters.from ?? ''} max={filters.to} onChange={(event) => change({ from: event.target.value || undefined })} />
        </label>
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
          {t('filters.to')}
          <Input type="date" value={filters.to ?? ''} min={filters.from} onChange={(event) => change({ to: event.target.value || undefined })} />
        </label>
      </div>
      {isError ? (
        <ErrorNotice message={t('error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={data?.items}
            rowKey={(l) => l.id}
            isLoading={isPending}
            caption={t('caption')}
            empty={<EmptyState title={t('empty.title')} description={t('empty.body')} />}
          />
          {data && data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}
