import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import '@/shared/lib/i18n'
import { DataTable, Pagination, type Column } from './data-table'

interface Row {
  id: string
  name: string
}

const columns: Column<Row>[] = [
  { key: 'name', header: 'Name', sortField: 'name', render: (row) => row.name },
  { key: 'id', header: 'Code', render: (row) => row.id },
]

const rows: Row[] = [
  { id: 'a', name: 'Rina' },
  { id: 'b', name: 'Budi' },
]

function renderTable(overrides: Partial<Parameters<typeof DataTable<Row>>[0]> = {}) {
  return render(
    <DataTable columns={columns} rows={rows} rowKey={(row) => row.id} isLoading={false} empty={<p>Nothing here</p>} caption="People" {...overrides} />,
  )
}

describe('DataTable', () => {
  it('shows a row per item under a captioned table', () => {
    renderTable()

    expect(screen.getByRole('table', { name: 'People' })).toBeTruthy()
    expect(screen.getAllByRole('row')).toHaveLength(3)
    expect(screen.getByText('Budi')).toBeTruthy()
  })

  it('shows a busy skeleton while loading, and no table', () => {
    renderTable({ isLoading: true })

    expect(screen.getByRole('status').getAttribute('aria-busy')).toBe('true')
    expect(screen.queryByRole('table')).toBeNull()
  })

  it.each([[[]], [undefined]])('shows the empty state when there are no rows (%j)', (empty) => {
    renderTable({ rows: empty })

    expect(screen.getByText('Nothing here')).toBeTruthy()
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('makes only columns with a sort field sortable, and says which way they are sorted', () => {
    renderTable({ sort: { field: 'name', desc: true }, onSortChange: vi.fn() })

    expect(screen.getByRole('button', { name: 'Name' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Code' })).toBeNull()
    expect(screen.getByRole('columnheader', { name: 'Name' }).getAttribute('aria-sort')).toBe('descending')
    expect(screen.getByRole('columnheader', { name: 'Code' }).getAttribute('aria-sort')).toBeNull()
  })

  it('sorts ascending on the first click of a column and flips on the next click of the same column', () => {
    const onSortChange = vi.fn()
    const { rerender } = renderTable({ sort: { field: 'other', desc: false }, onSortChange })

    fireEvent.click(screen.getByRole('button', { name: 'Name' }))
    expect(onSortChange).toHaveBeenLastCalledWith({ field: 'name', desc: false })

    rerender(
      <DataTable columns={columns} rows={rows} rowKey={(row) => row.id} isLoading={false} empty={null} caption="People" sort={{ field: 'name', desc: false }} onSortChange={onSortChange} />,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Name' }))
    expect(onSortChange).toHaveBeenLastCalledWith({ field: 'name', desc: true })
  })
})

describe('Pagination', () => {
  it('summarizes the visible range and the page', () => {
    render(<Pagination page={2} pageSize={20} totalCount={45} onPageChange={vi.fn()} />)

    expect(screen.getByText('21 to 40 of 45')).toBeTruthy()
    expect(screen.getByText('Page 2 of 3')).toBeTruthy()
  })

  it('cannot go back from the first page, and cannot go on from the last', () => {
    const { rerender } = render(<Pagination page={1} pageSize={20} totalCount={45} onPageChange={vi.fn()} />)
    expect((screen.getByRole('button', { name: 'Previous' }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement).disabled).toBe(false)

    rerender(<Pagination page={3} pageSize={20} totalCount={45} onPageChange={vi.fn()} />)
    expect((screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByText('41 to 45 of 45')).toBeTruthy()
  })

  it('asks for the previous and the next page', () => {
    const onPageChange = vi.fn()
    render(<Pagination page={2} pageSize={20} totalCount={60} onPageChange={onPageChange} />)

    fireEvent.click(screen.getByRole('button', { name: 'Previous' }))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(onPageChange.mock.calls).toEqual([[1], [3]])
  })

  it('shows 0 to 0 of 0 and a single page when there is nothing', () => {
    render(<Pagination page={1} pageSize={20} totalCount={0} onPageChange={vi.fn()} />)

    expect(screen.getByText('0 to 0 of 0')).toBeTruthy()
    expect(screen.getByText('Page 1 of 1')).toBeTruthy()
  })
})
