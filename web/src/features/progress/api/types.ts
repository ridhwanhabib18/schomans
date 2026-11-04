export type SummaryStatus = 'Draft' | 'Approved' | 'Sent'

export interface ProgressSummary {
  id: string
  studentId: string
  studentName: string
  /** `yyyy-MM`. */
  month: string
  body: string
  status: SummaryStatus
  isAiDrafted: boolean
  sentAt: string | null
}

export interface SentSummaries {
  sent: number
  parentsNotified: number
}

export interface MyClassroom {
  id: string
  name: string
  isHomeroom: boolean
}

export interface TeacherNote {
  id: string
  studentId: string
  authorName: string
  /** `yyyy-MM-dd`. */
  date: string
  body: string
  isMine: boolean
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export const NOTE_MAX_LENGTH = 500
export const SUMMARY_MAX_LENGTH = 3000
