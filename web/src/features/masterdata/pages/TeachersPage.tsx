import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TeacherDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { parseProblem } from '@/shared/lib/problem'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column, type SortState } from '@/shared/ui/data-table'
import { Input } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useDeleteTeacher, useTeachers } from '../api/masterdata'
import { RowActions } from '../components/RowActions'
import { TeacherFormDialog } from '../components/TeacherFormDialog'
import '../i18n'

const PAGE_SIZE = 20

export function TeachersPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const canEdit = useAuthStore(selectAuthRole) === 'Admin'
  const showToast = useToastStore(selectShowToast)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<SortState>({ field: 'name', desc: false })
  const debouncedSearch = useDebouncedValue(search)
  const [editing, setEditing] = useState<TeacherDto | 'new' | null>(null)
  const [deleting, setDeleting] = useState<TeacherDto | null>(null)
  const remove = useDeleteTeacher()

  const { data, isPending, isError, refetch } = useTeachers({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    Sort: sort.field,
    Desc: sort.desc,
  })

  const columns: Column<TeacherDto>[] = [
    { key: 'name', header: t('teachers.columns.name'), sortField: 'name', render: (x) => <span className="font-medium">{x.fullName}</span> },
    { key: 'nip', header: t('teachers.columns.nip'), sortField: 'nip', render: (x) => <span className="font-mono">{x.nip}</span> },
    { key: 'email', header: t('teachers.columns.email'), render: (x) => x.email },
    { key: 'homeroom', header: t('teachers.columns.homeroom'), render: (x) => (x.isHomeroomTeacher ? <Badge>{t('teachers.homeroom')}</Badge> : '—') },
    {
      key: 'account',
      header: t('teachers.columns.account'),
      render: (x) => <Badge variant={x.hasAccount ? 'default' : 'muted'}>{x.hasAccount ? t('common.accountYes') : t('common.accountNo')}</Badge>,
    },
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: t('common.actions'),
            className: 'text-right',
            render: (x: TeacherDto) => <RowActions name={x.fullName} onEdit={() => setEditing(x)} onDelete={() => setDeleting(x)} />,
          } satisfies Column<TeacherDto>,
        ]
      : []),
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('teachers.title')}
        description={t('teachers.description')}
        actions={
          canEdit && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden="true" />
              {t('teachers.add')}
            </Button>
          )
        }
      />

      <Input
        type="search"
        className="max-w-xs"
        placeholder={t('teachers.filter.search')}
        aria-label={t('teachers.filter.search')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value)
          setPage(1)
        }}
      />

      {isError ? (
        <ErrorNotice message={t('common.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('teachers.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(x) => x.id}
            isLoading={isPending}
            sort={sort}
            onSortChange={(next) => {
              setSort(next)
              setPage(1)
            }}
            empty={<EmptyState title={t('teachers.empty.title')} description={canEdit ? t('teachers.empty.description') : undefined} />}
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}

      {editing !== null && <TeacherFormDialog teacher={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('teachers.delete.title')}
        description={t('teachers.delete.body', { name: deleting?.fullName })}
        confirmLabel={t('teachers.delete.confirm')}
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
              showToast({ kind: 'error', title: t(`teachers.errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
              setDeleting(null)
            },
          })
        }
      />
    </div>
  )
}
