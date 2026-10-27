import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/shared/lib/utils'
import { Button } from './button'
import { Skeleton } from './skeleton'

export interface Column<T> {
  key: string
  header: string
  render: (row: T) => ReactNode
  /** The sort field the API understands. Without it the column is not sortable. */
  sortField?: string
  className?: string
}

export interface SortState {
  field: string
  desc: boolean
}

interface DataTableProps<T> {
  columns: readonly Column<T>[]
  rows: readonly T[] | undefined
  rowKey: (row: T) => string
  isLoading: boolean
  /** Shown instead of the table when there are no rows. */
  empty: ReactNode
  sort?: SortState
  onSortChange?: (sort: SortState) => void
  caption: string
}

/** A table fed by a paged API: sortable headers, a skeleton while loading, an empty state, and horizontal scrolling on a phone. */
export function DataTable<T>({ columns, rows, rowKey, isLoading, empty, sort, onSortChange, caption }: DataTableProps<T>) {
  const { t } = useTranslation()

  if (isLoading) {
    return (
      <div role="status" aria-busy="true" className="flex flex-col gap-2">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} className="h-11 w-full" />
        ))}
        <span className="sr-only">{t('states.loading')}</span>
      </div>
    )
  }

  if (!rows || rows.length === 0) {
    return <>{empty}</>
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full min-w-[40rem] text-left text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead className="bg-muted text-xs uppercase tracking-wide text-muted-foreground">
          <tr>
            {columns.map((column) => {
              const active = sort?.field === column.sortField
              return (
                <th
                  key={column.key}
                  scope="col"
                  aria-sort={column.sortField ? (active ? (sort?.desc ? 'descending' : 'ascending') : 'none') : undefined}
                  className={cn('px-4 py-3 font-medium', column.className)}
                >
                  {column.sortField && onSortChange ? (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1 uppercase tracking-wide hover:text-foreground"
                      onClick={() => onSortChange({ field: column.sortField!, desc: active ? !sort?.desc : false })}
                    >
                      {column.header}
                      {active && (sort?.desc ? <ArrowDown aria-hidden="true" className="size-3" /> : <ArrowUp aria-hidden="true" className="size-3" />)}
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody className="divide-y divide-border bg-card">
          {rows.map((row) => (
            <tr key={rowKey(row)} className="hover:bg-muted/50">
              {columns.map((column) => (
                <td key={column.key} className={cn('px-4 py-3 align-middle', column.className)}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

interface PaginationProps {
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
}

/** Previous and next with a "21 to 40 of 150" summary. */
export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const { t } = useTranslation()
  const pages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <nav aria-label={t('pagination.label')} className="flex flex-wrap items-center justify-between gap-3 text-sm">
      <p aria-live="polite" className="text-muted-foreground">
        {t('pagination.summary', { from, to, total: totalCount })}
      </p>
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          <ChevronLeft aria-hidden="true" />
          {t('pagination.previous')}
        </Button>
        <span className="text-muted-foreground">{t('pagination.page', { page, pages })}</span>
        <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => onPageChange(page + 1)}>
          {t('pagination.next')}
          <ChevronRight aria-hidden="true" />
        </Button>
      </div>
    </nav>
  )
}
