import { Pencil, Plus, Send, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { AnnouncementDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { formatDate } from '@/shared/lib/format'
import { isTargetRole } from '../audience'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { useToastStore, selectShowToast } from '@/stores/useToastStore'
import { parseProblem } from '@/shared/lib/problem'
import { useDeleteAnnouncement, useManagedAnnouncements, usePublishAnnouncement } from '../api/announcements'
import { AudienceText } from '../components/AudienceText'
import '../i18n'

const PAGE_SIZE = 20

export function AnnouncementsManagePage() {
  const { t } = useTranslation('announcements')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'' | 'Draft' | 'Published'>('')
  const debouncedSearch = useDebouncedValue(search)
  const [toPublish, setToPublish] = useState<AnnouncementDto | null>(null)
  const [toDelete, setToDelete] = useState<AnnouncementDto | null>(null)
  const publish = usePublishAnnouncement()
  const remove = useDeleteAnnouncement()

  const { data, isPending, isError, refetch } = useManagedAnnouncements({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    Status: status || undefined,
  })

  const columns: Column<AnnouncementDto>[] = [
    {
      key: 'title',
      header: t('manage.columns.title'),
      render: (a) => (
        <span className="flex items-center gap-2 font-medium">
          {a.title}
          {a.isPublic && <Badge variant="muted">{t('manage.public')}</Badge>}
        </span>
      ),
    },
    {
      key: 'status',
      header: t('manage.columns.status'),
      render: (a) => <Badge variant={a.status === 'Published' ? 'success' : 'warning'}>{t(`manage.status.${a.status}`)}</Badge>,
    },
    {
      key: 'audience',
      header: t('manage.columns.audience'),
      render: (a) => <AudienceText roles={a.targetRoles.filter(isTargetRole)} classroomCount={a.targetClassroomIds.length} />,
    },
    { key: 'published', header: t('manage.columns.published'), render: (a) => (a.publishedAt ? formatDate(a.publishedAt) : '—') },
    {
      key: 'actions',
      header: t('manage.columns.actions'),
      className: 'text-right',
      render: (a) => (
        <div className="flex justify-end gap-1">
          <Link to={`/manage/announcements/${a.id}`} className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
            <Pencil aria-hidden="true" />
            <span className="sr-only md:not-sr-only">{t('manage.edit')}</span>
            <span className="sr-only">: {a.title}</span>
          </Link>
          {a.status === 'Draft' && (
            <Button variant="ghost" size="sm" onClick={() => setToPublish(a)}>
              <Send aria-hidden="true" />
              <span className="sr-only md:not-sr-only">{t('manage.publish')}</span>
              <span className="sr-only">: {a.title}</span>
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={() => setToDelete(a)}>
            <Trash2 aria-hidden="true" />
            <span className="sr-only md:not-sr-only">{t('manage.delete')}</span>
            <span className="sr-only">: {a.title}</span>
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('manage.title')}
        description={t('manage.description')}
        actions={
          <Link to="/manage/announcements/new" className={buttonVariants()}>
            <Plus aria-hidden="true" />
            {t('manage.new')}
          </Link>
        }
      />

      <div className="flex flex-wrap gap-3">
        <Input
          type="search"
          className="max-w-xs"
          placeholder={t('manage.filter.search')}
          aria-label={t('manage.filter.search')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
        <Select
          className="max-w-48"
          aria-label={t('manage.columns.status')}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as typeof status)
            setPage(1)
          }}
        >
          <option value="">{t('manage.filter.all')}</option>
          <option value="Draft">{t('manage.status.Draft')}</option>
          <option value="Published">{t('manage.status.Published')}</option>
        </Select>
      </div>

      {isError ? (
        <ErrorNotice message={t('manage.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('manage.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(a) => a.id}
            isLoading={isPending}
            empty={<EmptyState title={t('manage.empty.title')} description={t('manage.empty.description')} />}
          />
          {data && data.totalCount > 0 && <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}

      <ConfirmDialog
        open={toPublish !== null}
        onOpenChange={(open) => !open && setToPublish(null)}
        title={t('form.publishTitle')}
        description={t('form.publishBody')}
        confirmLabel={t('form.publish')}
        pending={publish.isPending}
        onConfirm={() =>
          publish.mutate(toPublish!.id, {
            onSuccess: (result) => {
              showToast({ kind: 'success', title: t('form.published', { count: result.notifiedUsers }) })
              setToPublish(null)
            },
            onError: (error) => {
              showToast({ kind: 'error', title: parseProblem(error).title ?? tCommon('errors.generic') })
              setToPublish(null)
            },
          })
        }
      />

      <ConfirmDialog
        open={toDelete !== null}
        onOpenChange={(open) => !open && setToDelete(null)}
        title={t('delete.title')}
        description={t('delete.body', { title: toDelete?.title })}
        confirmLabel={t('delete.confirm')}
        destructive
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(toDelete!.id, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('delete.done') })
              setToDelete(null)
            },
            onError: (error) => {
              showToast({ kind: 'error', title: parseProblem(error).title ?? tCommon('errors.generic') })
              setToDelete(null)
            },
          })
        }
      />
    </div>
  )
}
