import { ExternalLink, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { NewsCategory, NewsDto } from '@/api/generated/model'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { formatDate } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useDeleteNews, useManagedNewsList } from '../api/news'
import '../i18n'

const PAGE_SIZE = 20
const CATEGORIES: readonly NonNullable<NewsCategory>[] = ['News', 'AdmissionInfo', 'Award']

export function NewsManagePage() {
  const { t } = useTranslation('news')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'' | 'Draft' | 'Published'>('')
  const [category, setCategory] = useState<'' | NonNullable<NewsCategory>>('')
  const debouncedSearch = useDebouncedValue(search)
  const [toDelete, setToDelete] = useState<NewsDto | null>(null)
  const remove = useDeleteNews()

  const { data, isPending, isError, refetch } = useManagedNewsList({
    Page: page,
    PageSize: PAGE_SIZE,
    Search: debouncedSearch || undefined,
    Status: status || undefined,
    Category: category || undefined,
  })

  const columns: Column<NewsDto>[] = [
    { key: 'title', header: t('manage.columns.title'), render: (a) => <span className="font-medium">{a.title}</span> },
    { key: 'category', header: t('manage.columns.category'), render: (a) => t(`categories.${a.category}`) },
    {
      key: 'status',
      header: t('manage.columns.status'),
      render: (a) => <Badge variant={a.status === 'Published' ? 'success' : 'warning'}>{t(`manage.status.${a.status}`)}</Badge>,
    },
    { key: 'published', header: t('manage.columns.published'), render: (a) => (a.publishedAt ? formatDate(a.publishedAt) : '—') },
    {
      key: 'actions',
      header: t('manage.columns.actions'),
      className: 'text-right',
      render: (a) => (
        <div className="flex justify-end gap-1">
          {a.status === 'Published' && (
            <Link to={`/news/${a.slug}`} className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
              <ExternalLink aria-hidden="true" />
              <span className="sr-only md:not-sr-only">{t('manage.view')}</span>
              <span className="sr-only">: {a.title}</span>
            </Link>
          )}
          <Link to={`/manage/news/${a.id}`} className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
            <Pencil aria-hidden="true" />
            <span className="sr-only md:not-sr-only">{t('manage.edit')}</span>
            <span className="sr-only">: {a.title}</span>
          </Link>
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
          <Link to="/manage/news/new" className={buttonVariants()}>
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
          <option value="">{t('manage.filter.allStatuses')}</option>
          <option value="Draft">{t('manage.status.Draft')}</option>
          <option value="Published">{t('manage.status.Published')}</option>
        </Select>
        <Select
          className="max-w-56"
          aria-label={t('manage.columns.category')}
          value={category}
          onChange={(event) => {
            setCategory(event.target.value as typeof category)
            setPage(1)
          }}
        >
          <option value="">{t('manage.filter.allCategories')}</option>
          {CATEGORIES.map((value) => (
            <option key={value} value={value}>
              {t(`categories.${value}`)}
            </option>
          ))}
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
