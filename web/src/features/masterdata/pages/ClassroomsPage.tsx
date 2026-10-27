import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ClassroomDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { parseProblem } from '@/shared/lib/problem'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column, type SortState } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcademicYears, useClassrooms, useDeleteClassroom } from '../api/masterdata'
import { ClassroomFormDialog } from '../components/ClassroomFormDialog'
import { RowActions } from '../components/RowActions'
import '../i18n'

const PAGE_SIZE = 20

export function ClassroomsPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const canEdit = useAuthStore(selectAuthRole) === 'Admin'
  const showToast = useToastStore(selectShowToast)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [yearId, setYearId] = useState('')
  const [sort, setSort] = useState<SortState>({ field: 'name', desc: false })
  const debouncedSearch = useDebouncedValue(search)
  const [editing, setEditing] = useState<ClassroomDto | 'new' | null>(null)
  const [deleting, setDeleting] = useState<ClassroomDto | null>(null)
  const remove = useDeleteClassroom()
  const years = useAcademicYears()

  const { data, isPending, isError, refetch } = useClassrooms({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    AcademicYearId: yearId || undefined,
    Sort: sort.field,
    Desc: sort.desc,
  })

  const columns: Column<ClassroomDto>[] = [
    { key: 'name', header: t('classrooms.columns.name'), sortField: 'name', render: (c) => <span className="font-medium">{c.name}</span> },
    { key: 'year', header: t('classrooms.columns.year'), sortField: 'academicYear', render: (c) => c.academicYearName },
    { key: 'homeroom', header: t('classrooms.columns.homeroom'), render: (c) => c.homeroomTeacherName },
    { key: 'students', header: t('classrooms.columns.students'), render: (c) => c.studentCount },
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: t('common.actions'),
            className: 'text-right',
            render: (c: ClassroomDto) => <RowActions name={c.name} onEdit={() => setEditing(c)} onDelete={() => setDeleting(c)} />,
          } satisfies Column<ClassroomDto>,
        ]
      : []),
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('classrooms.title')}
        description={t('classrooms.description')}
        actions={
          canEdit && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden="true" />
              {t('classrooms.add')}
            </Button>
          )
        }
      />

      <div className="flex flex-wrap gap-3">
        <Input
          type="search"
          className="max-w-xs"
          placeholder={t('classrooms.filter.search')}
          aria-label={t('classrooms.filter.search')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
        <Select
          className="max-w-56"
          aria-label={t('classrooms.columns.year')}
          value={yearId}
          onChange={(event) => {
            setYearId(event.target.value)
            setPage(1)
          }}
        >
          <option value="">{t('classrooms.filter.allYears')}</option>
          {years.data?.items.map((year) => (
            <option key={year.id} value={year.id}>
              {year.name}
            </option>
          ))}
        </Select>
      </div>

      {isError ? (
        <ErrorNotice message={t('common.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('classrooms.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(c) => c.id}
            isLoading={isPending}
            sort={sort}
            onSortChange={(next) => {
              setSort(next)
              setPage(1)
            }}
            empty={<EmptyState title={t('classrooms.empty.title')} description={canEdit ? t('classrooms.empty.description') : undefined} />}
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}

      {editing !== null && <ClassroomFormDialog classroom={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('classrooms.delete.title')}
        description={t('classrooms.delete.body', { name: deleting?.name })}
        confirmLabel={t('classrooms.delete.confirm')}
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
              showToast({ kind: 'error', title: t(`classrooms.errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
              setDeleting(null)
            },
          })
        }
      />
    </div>
  )
}
