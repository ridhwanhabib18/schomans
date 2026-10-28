import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { SubjectDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { parseProblem } from '@/shared/lib/problem'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column, type SortState } from '@/shared/ui/data-table'
import { Input } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useDeleteSubject, useSubjects } from '../api/masterdata'
import { RowActions } from '../components/RowActions'
import { SubjectFormDialog } from '../components/SubjectFormDialog'
import '../i18n'

const PAGE_SIZE = 20

export function SubjectsPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const canEdit = useAuthStore(selectAuthRole) === 'Admin'
  const showToast = useToastStore(selectShowToast)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<SortState>({ field: 'name', desc: false })
  const debouncedSearch = useDebouncedValue(search)
  const [editing, setEditing] = useState<SubjectDto | 'new' | null>(null)
  const [deleting, setDeleting] = useState<SubjectDto | null>(null)
  const remove = useDeleteSubject()

  const { data, isPending, isError, refetch } = useSubjects({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    Sort: sort.field,
    Desc: sort.desc,
  })

  const columns: Column<SubjectDto>[] = [
    { key: 'name', header: t('subjects.columns.name'), sortField: 'name', render: (s) => <span className="font-medium">{s.name}</span> },
    { key: 'score', header: t('subjects.columns.score'), sortField: 'minimumPassingScore', render: (s) => s.minimumPassingScore },
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: t('common.actions'),
            className: 'text-right',
            render: (s: SubjectDto) => <RowActions name={s.name} onEdit={() => setEditing(s)} onDelete={() => setDeleting(s)} />,
          } satisfies Column<SubjectDto>,
        ]
      : []),
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('subjects.title')}
        description={t('subjects.description')}
        actions={
          canEdit && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden="true" />
              {t('subjects.add')}
            </Button>
          )
        }
      />

      <Input
        type="search"
        className="max-w-xs"
        placeholder={t('subjects.filter.search')}
        aria-label={t('subjects.filter.search')}
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
            caption={t('subjects.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(s) => s.id}
            isLoading={isPending}
            sort={sort}
            onSortChange={(next) => {
              setSort(next)
              setPage(1)
            }}
            empty={<EmptyState title={t('subjects.empty.title')} description={canEdit ? t('subjects.empty.description') : undefined} />}
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}

      {editing !== null && <SubjectFormDialog subject={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('subjects.delete.title')}
        description={t('subjects.delete.body', { name: deleting?.name })}
        confirmLabel={t('subjects.delete.confirm')}
        destructive
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(deleting!.id, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('common.deleted') })
              setDeleting(null)
            },
            onError: (error) => {
              showToast({ kind: 'error', title: parseProblem(error).title ?? tCommon('errors.generic') })
              setDeleting(null)
            },
          })
        }
      />
    </div>
  )
}
