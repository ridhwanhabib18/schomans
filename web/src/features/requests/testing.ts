import type { ApprovalChain, RequestDetail, RequestListItem } from './api/types'

/** Fixtures of the request tests. */
export const listItem = (overrides: Partial<RequestListItem> = {}): RequestListItem => ({
  id: 'r1',
  type: 'StudentLeave',
  status: 'Pending',
  requesterName: 'Agus Putra',
  studentName: 'Rina Putri',
  startDate: '2026-11-02',
  endDate: '2026-11-04',
  reason: 'Family event',
  currentStep: 1,
  totalSteps: 2,
  waitingFor: 'HomeroomTeacher',
  createdAt: '2026-10-30T03:00:00Z',
  ...overrides,
})

export const detail = (overrides: Partial<RequestDetail> = {}): RequestDetail => ({
  id: 'r1',
  type: 'StudentLeave',
  status: 'Pending',
  requesterName: 'Agus Putra',
  studentId: 's1',
  studentName: 'Rina Putri',
  classroomName: 'Class A',
  startDate: '2026-11-02',
  endDate: '2026-11-04',
  reason: 'Family event',
  currentStep: 1,
  chain: ['HomeroomTeacher', 'Principal'],
  waitingFor: 'HomeroomTeacher',
  approvals: [],
  letter: null,
  canDecide: false,
  canCancel: false,
  createdAt: '2026-10-30T03:00:00Z',
  ...overrides,
})

export const chains: ApprovalChain[] = [
  { type: 'StudentLeave', roles: ['HomeroomTeacher', 'Principal'], hasPendingRequests: false },
  { type: 'TeacherLeave', roles: ['Principal'], hasPendingRequests: false },
  { type: 'CertificateLetter', roles: ['Admin', 'Principal'], hasPendingRequests: false },
]
