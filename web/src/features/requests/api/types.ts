// Hand-written until `npm run api:generate` is run against the API with the Phase 5 endpoints.

export const REQUEST_TYPES = ['StudentLeave', 'TeacherLeave', 'CertificateLetter'] as const
export type RequestType = (typeof REQUEST_TYPES)[number]

export const REQUEST_STATUSES = ['Pending', 'Approved', 'Rejected', 'Cancelled'] as const
export type RequestStatus = (typeof REQUEST_STATUSES)[number]

export const APPROVER_ROLES = ['HomeroomTeacher', 'Admin', 'Principal'] as const
export type ApproverRole = (typeof APPROVER_ROLES)[number]

export type ApprovalDecision = 'Approved' | 'Rejected'

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface RequestListItem {
  id: string
  type: RequestType
  status: RequestStatus
  requesterName: string
  studentName: string | null
  startDate: string | null
  endDate: string | null
  reason: string
  currentStep: number
  totalSteps: number
  waitingFor: ApproverRole | null
  createdAt: string
}

export interface Approval {
  step: number
  role: ApproverRole
  approverName: string
  decision: ApprovalDecision
  notes: string | null
  decidedAt: string
}

export interface LetterInfo {
  letterNumber: string
  issuedAt: string
}

export interface RequestDetail {
  id: string
  type: RequestType
  status: RequestStatus
  requesterName: string
  studentId: string | null
  studentName: string | null
  classroomName: string | null
  startDate: string | null
  endDate: string | null
  reason: string
  currentStep: number
  /** The roles of the chain, in the order they decide. */
  chain: ApproverRole[]
  waitingFor: ApproverRole | null
  approvals: Approval[]
  letter: LetterInfo | null
  canDecide: boolean
  canCancel: boolean
  createdAt: string
}

export interface ApprovalChain {
  type: RequestType
  roles: ApproverRole[]
  hasPendingRequests: boolean
}

export interface CreateRequestCommand {
  type: RequestType
  studentId: string | null
  startDate: string | null
  endDate: string | null
  reason: string
}

export interface LetterUrl {
  url: string
  expiresAt: string
  letterNumber: string
}

export interface LetterVerification {
  valid: boolean
  letterNumber: string | null
  studentName: string | null
  issuedOn: string | null
}
