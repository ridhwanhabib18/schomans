import { useTranslation } from 'react-i18next'
import { ChildPicker } from '@/shared/ui/child-picker'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useOwnStudentId } from '../api/reportCards'
import { StudentGradesView } from '../components/StudentGradesView'
import '../i18n'

/** A student's own published grades and report card. */
export function MyGradesPage() {
  const { t } = useTranslation('reportcards')
  const { data: studentId, isPending, isError, refetch } = useOwnStudentId()

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('mine.title')} description={t('mine.description')} />
      {isPending ? (
        <Skeleton className="h-10 w-64" />
      ) : isError ? (
        <ErrorNotice message={t('error')} onRetry={() => void refetch()} />
      ) : (
        <StudentGradesView studentId={studentId} />
      )}
    </div>
  )
}

/** A parent follows the published grades of a child. */
export function ChildGradesPage() {
  const { t } = useTranslation('reportcards')

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('child.title')} description={t('child.description')} />
      <ChildPicker>{(childId) => <StudentGradesView studentId={childId} />}</ChildPicker>
    </div>
  )
}
