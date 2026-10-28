import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ChildPicker } from '@/shared/ui/child-picker'
import { PageHeader } from '@/shared/ui/page-parts'
import { useChildAssignments, useMyAssignments } from '../api/assignments'
import { StudentAssignmentList } from '../components/StudentAssignmentList'
import '../i18n'

/** PRD US-02: a student sees their assignments and due dates. */
export function StudentAssignmentsPage() {
  const { t } = useTranslation('assignments')
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useMyAssignments(page)

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('student.title')} description={t('student.description')} />
      <StudentAssignmentList data={data} isPending={isPending} isError={isError} onRetry={() => void refetch()} onPageChange={setPage} />
    </div>
  )
}

function ChildList({ childId }: { childId: string }) {
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useChildAssignments(childId, page)
  return <StudentAssignmentList data={data} isPending={isPending} isError={isError} onRetry={() => void refetch()} onPageChange={setPage} childId={childId} />
}

/** A parent follows a child's assignments and scores (authorization matrix: own child, read only). */
export function ChildAssignmentsPage() {
  const { t } = useTranslation('assignments')

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('child.title')} description={t('child.description')} />
      <ChildPicker>{(childId) => <ChildList key={childId} childId={childId} />}</ChildPicker>
    </div>
  )
}
