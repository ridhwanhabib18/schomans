import { useQuery } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import type { AuditFilters, AuditLog, Paged } from './types'

export const PAGE_SIZE = 20

export const auditKeys = {
  all: ['audit-logs'] as const,
  list: (filters: AuditFilters, page: number) => [...auditKeys.all, filters.entityName ?? 'all', filters.from ?? '', filters.to ?? '', page] as const,
}

const list = (filters: AuditFilters, page: number) =>
  customInstance<Paged<AuditLog>>({
    url: '/api/v1/audit-logs',
    method: 'GET',
    params: { entityName: filters.entityName, from: filters.from, to: filters.to, page, pageSize: PAGE_SIZE },
  })

/** The audit trail, newest first. */
export function useAuditLogs(filters: AuditFilters, page: number) {
  return useQuery({ queryKey: auditKeys.list(filters, page), queryFn: () => list(filters, page) })
}
