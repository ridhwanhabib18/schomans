import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { Alert } from '@/shared/ui/alert'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useAssignment, useTeachingOptions } from '../api/assignments'
import { AssignmentForm } from '../components/AssignmentForm'
import '../i18n'

function Editor({ id }: { id: string | undefined }) {
  const { t } = useTranslation('assignments')
  const options = useTeachingOptions()
  const assignment = useAssignment(id ?? '')
  const loading = options.isPending || (id !== undefined && assignment.isPending)

  if (loading) {
    return (
      <div role="status" aria-busy="true" className="flex max-w-3xl flex-col gap-3">
        <Skeleton className="h-10" />
        <Skeleton className="h-40" />
      </div>
    )
  }

  if (options.isError || (id !== undefined && assignment.isError)) {
    return <ErrorNotice message={t('detail.error')} onRetry={() => void (options.isError ? options.refetch() : assignment.refetch())} />
  }

  if (id === undefined && options.data!.length === 0) {
    return <Alert tone="info" message={t('form.noOptions')} />
  }

  const data = id === undefined ? undefined : assignment.data
  return (
    <AssignmentForm
      key={data ? `${data.id}-${data.rowVersion}` : 'new'}
      assignment={data}
      options={options.data!}
      onReload={() => void assignment.refetch()}
    />
  )
}

/** `/assignments/new` gives a new assignment; `/assignments/:id/edit` changes one. */
export function AssignmentEditorPage() {
  const { t } = useTranslation('assignments')
  const { id } = useParams()

  return (
    <div className="flex flex-col gap-6">
      <Link to={id ? `/assignments/${id}` : '/assignments'} className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('form.back')}
      </Link>
      <PageHeader title={id ? t('form.editTitle') : t('form.newTitle')} />
      <Editor id={id} />
    </div>
  )
}
