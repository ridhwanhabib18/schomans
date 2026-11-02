import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { Pagination } from '@/shared/ui/data-table'
import { Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { PAGE_SIZE, useAllRequests, useMyRequests, useRequestInbox } from '../api/requests'
import { REQUEST_STATUSES, REQUEST_TYPES, type RequestStatus, type RequestType } from '../api/types'
import { RequestTable } from '../components/RequestParts'
import '../i18n'

/** What a parent, a student or a teacher made, and what was made about their child or themselves. */
export function MyRequestsPage() {
  const { t } = useTranslation('requests')
  const [status, setStatus] = useState<RequestStatus | undefined>()
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useMyRequests(status, page)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader
        title={t('mine.title')}
        description={t('mine.description')}
        actions={
          <Link to="/requests/new" className={buttonVariants()}>
            <Plus aria-hidden="true" />
            {t('mine.new')}
          </Link>
        }
      />
      <label className="flex max-w-xs flex-col gap-1.5 text-sm font-medium">
        {t('mine.filter')}
        <Select
          value={status ?? ''}
          onChange={(event) => {
            setStatus((event.target.value || undefined) as RequestStatus | undefined)
            setPage(1)
          }}
        >
          <option value="">{t('mine.allStatuses')}</option>
          {REQUEST_STATUSES.map((s) => (
            <option key={s} value={s}>
              {t(`status.${s}`)}
            </option>
          ))}
        </Select>
      </label>
      {isError ? (
        <ErrorNotice message={t('mine.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <RequestTable
            rows={data?.items}
            isLoading={isPending}
            caption={t('mine.caption')}
            empty={<EmptyState title={t('mine.empty.title')} description={t('mine.empty.body')} />}
            showRequester={false}
          />
          {data && data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}

/** The requests that wait for the decision of the signed-in approver. */
export function RequestInboxPage() {
  const { t } = useTranslation('requests')
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useRequestInbox(page)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('inbox.title')} description={t('inbox.description')} />
      {isError ? (
        <ErrorNotice message={t('inbox.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <RequestTable
            rows={data?.items}
            isLoading={isPending}
            caption={t('inbox.caption')}
            empty={<EmptyState title={t('inbox.empty.title')} description={t('inbox.empty.body')} />}
          />
          {data && data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}

/** Every request of the school, for the staff who oversee them. */
export function AllRequestsPage() {
  const { t } = useTranslation('requests')
  const [status, setStatus] = useState<RequestStatus | undefined>()
  const [type, setType] = useState<RequestType | undefined>()
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useAllRequests(status, type, page)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('all.title')} description={t('all.description')} />
      <div className="flex flex-wrap gap-4">
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
          {t('all.status')}
          <Select
            value={status ?? ''}
            onChange={(event) => {
              setStatus((event.target.value || undefined) as RequestStatus | undefined)
              setPage(1)
            }}
          >
            <option value="">{t('all.allStatuses')}</option>
            {REQUEST_STATUSES.map((s) => (
              <option key={s} value={s}>
                {t(`status.${s}`)}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
          {t('all.type')}
          <Select
            value={type ?? ''}
            onChange={(event) => {
              setType((event.target.value || undefined) as RequestType | undefined)
              setPage(1)
            }}
          >
            <option value="">{t('all.allTypes')}</option>
            {REQUEST_TYPES.map((k) => (
              <option key={k} value={k}>
                {t(`type.${k}`)}
              </option>
            ))}
          </Select>
        </label>
      </div>
      {isError ? (
        <ErrorNotice message={t('all.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <RequestTable
            rows={data?.items}
            isLoading={isPending}
            caption={t('all.caption')}
            empty={<EmptyState title={t('all.empty.title')} description={t('all.empty.body')} />}
          />
          {data && data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </div>
  )
}
