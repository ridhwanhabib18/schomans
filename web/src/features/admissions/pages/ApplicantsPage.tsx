import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { formatDateTime } from '@/shared/lib/format'
import { buttonVariants } from '@/shared/ui/button'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { APPLICANT_STATUSES, useApplicants, type ApplicantListItem, type ApplicantStatus } from '../api/admissions'
import { ApplicantStatusBadge } from '../components/StatusBadges'
import '../i18n'

const PAGE_SIZE = 20

/** The admin staff's list of applications: handed in and later (drafts are not shown), with a status filter and a search. */
export function ApplicantsPage() {
  const { t } = useTranslation('admissions')
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'' | ApplicantStatus>('')
  const debouncedSearch = useDebouncedValue(search)
  const { data, isPending, isError, refetch } = useApplicants({ page, pageSize: PAGE_SIZE, status: status || undefined, search: debouncedSearch || undefined })

  const columns: Column<ApplicantListItem>[] = [
    { key: 'name', header: t('staff.columns.name'), render: (a) => <span className="font-medium">{a.fullName}</span> },
    { key: 'email', header: t('staff.columns.email'), render: (a) => a.email },
    { key: 'status', header: t('staff.columns.status'), render: (a) => <ApplicantStatusBadge status={a.status} /> },
    {
      key: 'documents',
      header: t('staff.columns.documents'),
      className: 'tabular-nums',
      render: (a) => t('staff.documentsOf', { verified: a.verifiedDocuments, total: a.totalDocuments }),
    },
    { key: 'submitted', header: t('staff.columns.submitted'), render: (a) => (a.submittedAt ? formatDateTime(a.submittedAt) : '—') },
    { key: 'period', header: t('staff.columns.period'), render: (a) => a.periodName },
    {
      key: 'actions',
      header: '',
      className: 'text-right',
      render: (a) => (
        <Link to={`/manage/admissions/${a.id}`} className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
          {t('staff.open')}
          <span className="sr-only">: {a.fullName}</span>
        </Link>
      ),
    },
  ]

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('staff.title')} description={t('staff.description')} />

      <div className="flex flex-wrap gap-3">
        <Input
          type="search"
          className="max-w-xs"
          placeholder={t('staff.search')}
          aria-label={t('staff.search')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setPage(1)
          }}
        />
        <Select
          className="max-w-52"
          aria-label={t('staff.filterStatus')}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as typeof status)
            setPage(1)
          }}
        >
          <option value="">{t('staff.allStatuses')}</option>
          {APPLICANT_STATUSES.map((value) => (
            <option key={value} value={value}>
              {t(`status.${value}`)}
            </option>
          ))}
        </Select>
      </div>

      {isError ? (
        <ErrorNotice message={t('staff.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            caption={t('staff.caption')}
            columns={columns}
            rows={data?.items}
            rowKey={(a) => a.id}
            isLoading={isPending}
            empty={<EmptyState title={t('staff.empty.title')} description={t('staff.empty.description')} />}
          />
          {data && data.totalCount > 0 && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}
