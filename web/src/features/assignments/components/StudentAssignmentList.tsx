import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { PagedResultOfStudentAssignmentDto } from '@/api/generated/model'
import { formatDateTime } from '@/shared/lib/format'
import { Pagination } from '@/shared/ui/data-table'
import { EmptyState, ErrorNotice } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { AssignmentStatusBadge } from './AssignmentStatusBadge'
import '../i18n'

interface StudentAssignmentListProps {
  data: PagedResultOfStudentAssignmentDto | undefined
  isPending: boolean
  isError: boolean
  onRetry: () => void
  onPageChange: (page: number) => void
  /** Added to the links for a parent, so the detail page knows which child to show. */
  childId?: string
}

/** A student's assignments as cards, nearest due date first, each with its status and score. */
export function StudentAssignmentList({ data, isPending, isError, onRetry, onPageChange, childId }: StudentAssignmentListProps) {
  const { t } = useTranslation('assignments')

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="flex flex-col gap-3">
        <Skeleton className="h-20" />
        <Skeleton className="h-20" />
      </div>
    )
  }

  if (isError || !data) {
    return <ErrorNotice message={t('student.error')} onRetry={onRetry} />
  }

  if (data.items.length === 0) {
    return <EmptyState title={t('student.empty.title')} description={t('student.empty.description')} />
  }

  return (
    <div className="flex flex-col gap-4">
      <ul aria-label={t('student.caption')} className="flex flex-col gap-3">
        {data.items.map((assignment) => (
          <li key={assignment.id}>
            <Link
              to={childId ? `/assignments/${assignment.id}?child=${childId}` : `/assignments/${assignment.id}`}
              className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-card p-4 transition-colors hover:bg-muted/50"
            >
              <div className="min-w-0">
                <h2 className="font-semibold">{assignment.title}</h2>
                <p className="text-sm text-muted-foreground">{t('student.by', { subject: assignment.subjectName, teacher: assignment.teacherName })}</p>
                <p className="text-sm">{t('student.due', { date: formatDateTime(assignment.dueAt) })}</p>
              </div>
              <div className="flex items-center gap-2">
                {assignment.score !== null && <span className="font-semibold tabular-nums">{t('status.scored', { score: assignment.score })}</span>}
                <AssignmentStatusBadge status={assignment.status} overdue={assignment.isOverdue} />
              </div>
            </Link>
          </li>
        ))}
      </ul>
      <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={onPageChange} />
    </div>
  )
}
