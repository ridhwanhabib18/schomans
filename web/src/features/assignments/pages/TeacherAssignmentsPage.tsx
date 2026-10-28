import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { AssignmentListItemDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { formatDateTime } from '@/shared/lib/format'
import { buttonVariants } from '@/shared/ui/button'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { useTeacherAssignments } from '../api/assignments'
import '../i18n'

/** The assignments a teacher gave (every assignment for the principal), with how many students handed in. */
export function TeacherAssignmentsPage() {
  const { t } = useTranslation('assignments')
  const isTeacher = useAuthStore(selectAuthRole) === 'Teacher'
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const debounced = useDebouncedValue(search)
  const { data, isPending, isError, refetch } = useTeacherAssignments({ Page: page, PageSize: 20, Search: debounced || undefined })

  const columns: Column<AssignmentListItemDto>[] = [
    {
      key: 'title',
      header: t('teacher.columns.title'),
      render: (a) => (
        <Link to={`/assignments/${a.id}`} className="font-medium text-primary hover:underline">
          {a.title}
        </Link>
      ),
    },
    { key: 'subject', header: t('teacher.columns.subject'), render: (a) => a.subjectName },
    { key: 'classrooms', header: t('teacher.columns.classrooms'), render: (a) => a.classroomNames.join(', ') },
    { key: 'due', header: t('teacher.columns.due'), render: (a) => formatDateTime(a.dueAt) },
    { key: 'progress', header: t('teacher.columns.progress'), className: 'tabular-nums', render: (a) => t('teacher.progress', { submitted: a.submittedCount, total: a.studentCount }) },
    { key: 'graded', header: t('teacher.columns.graded'), className: 'tabular-nums', render: (a) => a.gradedCount },
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('teacher.title')}
        description={isTeacher ? t('teacher.description') : t('teacher.descriptionPrincipal')}
        actions={
          isTeacher && (
            <Link to="/assignments/new" className={buttonVariants()}>
              <Plus aria-hidden="true" />
              {t('teacher.new')}
            </Link>
          )
        }
      />
      <Input
        type="search"
        className="max-w-xs"
        placeholder={t('teacher.search')}
        aria-label={t('teacher.search')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value)
          setPage(1)
        }}
      />
      {isError ? (
        <ErrorNotice message={t('teacher.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('teacher.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(a) => a.id}
            isLoading={isPending}
            empty={<EmptyState title={t('teacher.empty.title')} description={isTeacher ? t('teacher.empty.description') : undefined} />}
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}
