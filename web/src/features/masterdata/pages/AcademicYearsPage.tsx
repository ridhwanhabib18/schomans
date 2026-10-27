import { CalendarPlus, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { AcademicYearDto } from '@/api/generated/model'
import { formatDate } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, type Column } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcademicYears, useActivateAcademicYear } from '../api/masterdata'
import { AcademicYearFormDialog } from '../components/AcademicYearFormDialog'
import '../i18n'

export function AcademicYearsPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const canEdit = useAuthStore(selectAuthRole) === 'Admin'
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError, refetch } = useAcademicYears()
  const [editing, setEditing] = useState<AcademicYearDto | 'new' | null>(null)
  const [activating, setActivating] = useState<AcademicYearDto | null>(null)
  const activate = useActivateAcademicYear()

  const columns: Column<AcademicYearDto>[] = [
    { key: 'name', header: t('years.columns.name'), render: (y) => <span className="font-medium">{y.name}</span> },
    { key: 'period', header: t('years.columns.period'), render: (y) => `${formatDate(y.startDate)} – ${formatDate(y.endDate)}` },
    { key: 'terms', header: t('years.columns.terms'), render: (y) => y.terms.map((term) => term.name).join(', ') },
    { key: 'classrooms', header: t('years.columns.classrooms'), render: (y) => y.classroomCount },
    {
      key: 'status',
      header: t('years.columns.status'),
      render: (y) => (y.isActive ? <Badge variant="success">{t('years.current')}</Badge> : <Badge variant="muted">—</Badge>),
    },
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: t('common.actions'),
            className: 'text-right',
            render: (y: AcademicYearDto) => (
              <div className="flex justify-end gap-1">
                <Button variant="ghost" size="sm" onClick={() => setEditing(y)}>
                  <Pencil aria-hidden="true" />
                  <span className="sr-only md:not-sr-only">{tCommon('actions.edit')}</span>
                  <span className="sr-only">: {y.name}</span>
                </Button>
                {!y.isActive && (
                  <Button variant="ghost" size="sm" onClick={() => setActivating(y)}>
                    {t('years.activate')}
                    <span className="sr-only">: {y.name}</span>
                  </Button>
                )}
              </div>
            ),
          } satisfies Column<AcademicYearDto>,
        ]
      : []),
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('years.title')}
        description={t('years.description')}
        actions={
          canEdit && (
            <div className="flex flex-wrap gap-2">
              <Link to="/manage/academic-years/rollover" className={buttonVariants({ variant: 'outline' })}>
                <CalendarPlus aria-hidden="true" />
                {t('years.rollover')}
              </Link>
              <Button onClick={() => setEditing('new')}>
                <Plus aria-hidden="true" />
                {t('years.add')}
              </Button>
            </div>
          )
        }
      />

      {isError ? (
        <ErrorNotice message={t('common.error')} onRetry={() => void refetch()} />
      ) : (
        <DataTable
          caption={t('years.caption')}
          columns={columns}
          rows={data?.items}
          rowKey={(y) => y.id}
          isLoading={isPending}
          empty={<EmptyState title={t('years.empty.title')} description={canEdit ? t('years.empty.description') : undefined} />}
        />
      )}

      {editing !== null && <AcademicYearFormDialog year={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={activating !== null}
        onOpenChange={(open) => !open && setActivating(null)}
        title={t('years.activateTitle', { name: activating?.name })}
        description={t('years.activateBody')}
        confirmLabel={t('years.activate')}
        pending={activate.isPending}
        onConfirm={() =>
          activate.mutate(activating!.id, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('years.activated', { name: activating?.name }) })
              setActivating(null)
            },
            onError: (error) => {
              showToast({ kind: 'error', title: parseProblem(error).title ?? tCommon('errors.generic') })
              setActivating(null)
            },
          })
        }
      />
    </div>
  )
}
