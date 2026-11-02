import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAdmissionPeriods, useDeletePeriod, type AdmissionPeriod } from '../api/admissions'
import { PeriodFormDialog } from '../components/PeriodFormDialog'
import { admissionErrorText } from '../errors'
import '../i18n'

/** The admin staff's admission periods: when applications open and close, when the results come, and the re-registration deadline. */
export function AdmissionPeriodsPage() {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError, refetch } = useAdmissionPeriods()
  const remove = useDeletePeriod()
  const [editing, setEditing] = useState<AdmissionPeriod | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<AdmissionPeriod | null>(null)

  const columns: Column<AdmissionPeriod>[] = [
    { key: 'name', header: t('periods.columns.name'), render: (p) => <span className="font-medium">{p.name}</span> },
    { key: 'year', header: t('periods.columns.year'), render: (p) => p.academicYearName },
    { key: 'window', header: t('periods.columns.window'), render: (p) => `${formatDateTime(p.opensAt)} – ${formatDateTime(p.closesAt)}` },
    { key: 'results', header: t('periods.columns.results'), render: (p) => formatDateTime(p.resultsAnnouncedAt) },
    { key: 'deadline', header: t('periods.columns.deadline'), render: (p) => formatDateTime(p.reregistrationDeadline) },
    { key: 'applicants', header: t('periods.columns.applicants'), className: 'tabular-nums', render: (p) => p.applicantCount },
    {
      key: 'actions',
      header: t('periods.columns.actions'),
      className: 'text-right',
      render: (p) => (
        <div className="flex justify-end gap-1">
          <Button variant="ghost" size="sm" onClick={() => setEditing(p)}>
            <Pencil aria-hidden="true" />
            <span className="sr-only md:not-sr-only">{t('periods.edit')}</span>
            <span className="sr-only">: {p.name}</span>
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setToDelete(p)}>
            <Trash2 aria-hidden="true" />
            <span className="sr-only md:not-sr-only">{t('periods.delete')}</span>
            <span className="sr-only">: {p.name}</span>
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader
        title={t('periods.title')}
        description={t('periods.description')}
        actions={
          <Button onClick={() => setEditing('new')}>
            <Plus aria-hidden="true" />
            {t('periods.add')}
          </Button>
        }
      />

      {isError ? (
        <ErrorNotice message={t('periods.error')} onRetry={() => void refetch()} />
      ) : (
        <DataTable
          caption={t('periods.caption')}
          columns={columns}
          rows={data}
          rowKey={(p) => p.id}
          isLoading={isPending}
          empty={<EmptyState title={t('periods.empty.title')} description={t('periods.empty.description')} />}
        />
      )}

      {editing !== null && <PeriodFormDialog period={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={toDelete !== null}
        onOpenChange={(open) => !open && setToDelete(null)}
        title={t('periods.deleteTitle', { name: toDelete?.name })}
        description={t('periods.deleteBody')}
        confirmLabel={t('periods.delete')}
        destructive
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(toDelete!.id, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('periods.deleted') })
              setToDelete(null)
            },
            onError: (e) => {
              showToast({ kind: 'error', title: admissionErrorText(e, t, tCommon('errors.generic')) })
              setToDelete(null)
            },
          })
        }
      />
    </div>
  )
}
