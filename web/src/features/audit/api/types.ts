export const AUDIT_ENTITIES = ['Grade', 'AttendanceRecord', 'Request', 'Approval'] as const
export type AuditEntity = (typeof AUDIT_ENTITIES)[number]

export type AuditAction = 'Created' | 'Updated' | 'Deleted'

export interface AuditLog {
  id: string
  at: string
  userId: string | null
  userName: string | null
  action: AuditAction
  entityName: string
  entityId: string
  /** JSON of the values before the change; null for a new row. */
  oldValues: string | null
  /** JSON of the values after the change; null for a deleted row. */
  newValues: string | null
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface AuditFilters {
  entityName: AuditEntity | undefined
  from: string | undefined
  to: string | undefined
}
