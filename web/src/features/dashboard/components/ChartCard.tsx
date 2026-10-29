import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'

interface ChartCardProps {
  title: string
  description?: string
  /** What the chart says, in words, for people who cannot see it. */
  summary: string
  /** The same numbers as a table: read by screen readers instead of the drawing. */
  table: ReactNode
  children: ReactNode
}

/** A titled card around a chart. The drawing is hidden from assistive technology; the table next to it carries the data. */
export function ChartCard({ title, description, summary, table, children }: ChartCardProps) {
  return (
    <Card className="min-w-0">
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>
        <div role="img" aria-label={summary} className="h-72 w-full min-w-0 overflow-hidden">
          {children}
        </div>
        {table}
      </CardContent>
    </Card>
  )
}

/**
 * A data table that only screen readers see. The hiding class sits on a wrapper, not on the table: a table ignores a
 * 1px width and overflow, keeps its full width, and would widen the page on a phone.
 */
export function DataTableForReaders({ caption, headers, rows }: { caption: string; headers: string[]; rows: ReactNode[][] }) {
  return (
    <div className="sr-only">
      <table>
        <caption>{caption}</caption>
        <thead>
          <tr>
            {headers.map((header) => (
              <th key={header} scope="col">
                {header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {row.map((cell, cellIndex) => (
                <td key={cellIndex}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
