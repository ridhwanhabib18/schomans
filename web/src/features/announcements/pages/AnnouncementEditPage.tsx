import { ArrowLeft } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useManagedAnnouncement } from '../api/announcements'
import { AnnouncementForm } from '../components/AnnouncementForm'
import '../i18n'

/** `/manage/announcements/new` writes a new announcement; `/manage/announcements/:id` edits a saved one. */
export function AnnouncementEditPage() {
  const { t } = useTranslation('announcements')
  const { id } = useParams()
  const { data, isPending, isError, refetch } = useManagedAnnouncement(id)
  const loading = id !== undefined && isPending

  return (
    <div className="flex flex-col gap-6">
      <Link to="/manage/announcements" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('manage.title')}
      </Link>
      <PageHeader title={id ? t('form.editTitle') : t('form.newTitle')} />

      {loading ? (
        <div role="status" aria-busy="true" className="flex max-w-3xl flex-col gap-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('form.loadError')} onRetry={() => void refetch()} />
      ) : (
        // A new version (after a save or a reload) is a new key: the form starts again from the saved data.
        <AnnouncementForm key={data ? `${data.id}-${data.rowVersion}` : 'new'} announcement={data} onReload={() => void refetch()} />
      )}
    </div>
  )
}
