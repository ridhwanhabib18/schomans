import { FileSpreadsheet, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StudentDto, StudentStatus } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { formatDate } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column, type SortState } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useActiveClassrooms, useDeleteStudent, useStudents } from '../api/masterdata'
import { ImportStudentsDialog } from '../components/ImportStudentsDialog'
import { RowActions } from '../components/RowActions'
import { StudentFormDialog } from '../components/StudentFormDialog'
import '../i18n'

const PAGE_SIZE = 20

/**
 * The student list: search, filter by classroom and status, sort, page. Admin staff add, edit, delete and import;
 * the principal sees everything; a teacher sees only their own classroom (the API enforces that, not this page).
 */
export function StudentsPage() {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const role = useAuthStore(selectAuthRole)
  const canEdit = role === 'Admin'
  const showToast = useToastStore(selectShowToast)

  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [classroomId, setClassroomId] = useState('')
  const [status, setStatus] = useState<'' | NonNullable<StudentStatus>>('Active')
  const [sort, setSort] = useState<SortState>({ field: 'name', desc: false })
  const debouncedSearch = useDebouncedValue(search)

  const [editing, setEditing] = useState<StudentDto | 'new' | null>(null)
  const [importing, setImporting] = useState(false)
  const [deleting, setDeleting] = useState<StudentDto | null>(null)
  const remove = useDeleteStudent()
  const classrooms = useActiveClassrooms()

  const { data, isPending, isError, refetch } = useStudents({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    ClassroomId: classroomId || undefined,
    Status: status || undefined,
    Sort: sort.field,
    Desc: sort.desc,
  })

  const filtered = Boolean(debouncedSearch || classroomId)

  const columns: Column<StudentDto>[] = [
    { key: 'name', header: t('students.columns.name'), sortField: 'name', render: (s) => <span className="font-medium">{s.fullName}</span> },
    { key: 'nisn', header: t('students.columns.nisn'), sortField: 'nisn', render: (s) => <span className="font-mono">{s.nisn}</span> },
    { key: 'email', header: t('students.columns.email'), render: (s) => s.email },
    { key: 'classroom', header: t('students.columns.classroom'), sortField: 'classroom', render: (s) => s.classroomName },
    { key: 'dateOfBirth', header: t('students.columns.dateOfBirth'), sortField: 'dateOfBirth', render: (s) => formatDate(s.dateOfBirth) },
    {
      key: 'status',
      header: t('students.columns.status'),
      render: (s) => <Badge variant={s.status === 'Active' ? 'success' : 'muted'}>{t(`students.status.${s.status}`)}</Badge>,
    },
    {
      key: 'account',
      header: t('students.columns.account'),
      render: (s) => <Badge variant={s.hasAccount ? 'default' : 'muted'}>{s.hasAccount ? t('common.accountYes') : t('common.accountNo')}</Badge>,
    },
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: t('common.actions'),
            className: 'text-right',
            render: (s: StudentDto) => <RowActions name={s.fullName} onEdit={() => setEditing(s)} onDelete={() => setDeleting(s)} />,
          } satisfies Column<StudentDto>,
        ]
      : []),
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('students.title')}
        description={role === 'Teacher' ? t('students.descriptionTeacher') : t('students.description')}
        actions={
          canEdit && (
            <>
              <Button variant="outline" onClick={() => setImporting(true)}>
                <FileSpreadsheet aria-hidden="true" />
                {t('students.import')}
              </Button>
              <Button onClick={() => setEditing('new')}>
                <Plus aria-hidden="true" />
                {t('students.add')}
              </Button>
            </>
          )
        }
      />

      <div className="flex flex-wrap gap-3">
        <Input
          type="search"
          className="max-w-xs"
          placeholder={t('students.filter.search')}
          aria-label={t('students.filter.search')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
        {role !== 'Teacher' && (
          <Select
            className="max-w-48"
            aria-label={t('students.columns.classroom')}
            value={classroomId}
            onChange={(event) => {
              setClassroomId(event.target.value)
              setPage(1)
            }}
          >
            <option value="">{t('common.allClassrooms')}</option>
            {classrooms.data?.map((classroom) => (
              <option key={classroom.id} value={classroom.id}>
                {classroom.name}
              </option>
            ))}
          </Select>
        )}
        <Select
          className="max-w-48"
          aria-label={t('students.columns.status')}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as typeof status)
            setPage(1)
          }}
        >
          <option value="">{t('common.allStatuses')}</option>
          <option value="Active">{t('students.status.Active')}</option>
          <option value="Graduated">{t('students.status.Graduated')}</option>
        </Select>
      </div>

      {isError ? (
        <ErrorNotice message={t('common.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('students.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(s) => s.id}
            isLoading={isPending}
            sort={sort}
            onSortChange={(next) => {
              setSort(next)
              setPage(1)
            }}
            empty={
              <EmptyState
                title={t('students.empty.title')}
                description={filtered ? t('students.empty.descriptionFiltered') : canEdit ? t('students.empty.description') : undefined}
              />
            }
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}

      {editing !== null && <StudentFormDialog student={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
      {importing && <ImportStudentsDialog onClose={() => setImporting(false)} />}

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('students.delete.title')}
        description={t('students.delete.body', { name: deleting?.fullName })}
        confirmLabel={t('students.delete.confirm')}
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
