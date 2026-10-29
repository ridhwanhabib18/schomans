import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listAcademicYears } from '@/api/generated/schomans'
import { openInNewTab } from '@/shared/lib/download'
import { customInstance } from '@/shared/lib/http'
import { putToPresignedUrl } from '@/shared/lib/upload'

// Hand-written until `npm run api:generate` is run against the API with the Phase 5 endpoints.

export type ApplicantStatus = 'Draft' | 'Submitted' | 'InVerification' | 'Accepted' | 'Rejected' | 'Enrolled'
export type DocumentType = 'BirthCertificate' | 'FamilyCard' | 'ReportCard'
export type DocumentStatus = 'Pending' | 'Verified' | 'Rejected'

export const DOCUMENT_KINDS: readonly DocumentType[] = ['BirthCertificate', 'FamilyCard', 'ReportCard']
export const APPLICANT_STATUSES: readonly ApplicantStatus[] = ['Draft', 'Submitted', 'InVerification', 'Accepted', 'Rejected', 'Enrolled']

export interface PublicAdmissionPeriod {
  id: string
  name: string
  opensAt: string
  closesAt: string
  resultsAnnouncedAt: string
  reregistrationDeadline: string
  isOpen: boolean
}

export interface ApplicantForm {
  fullName: string
  dateOfBirth: string | null
  placeOfBirth: string
  address: string
  guardianName: string
  guardianPhone: string
  previousSchool: string
}

export interface ApplicantDocument {
  type: DocumentType
  fileName: string
  status: DocumentStatus
  reviewNote: string | null
}

export interface MyApplication {
  id: string
  status: ApplicantStatus
  form: ApplicantForm
  notes: string | null
  submittedAt: string | null
  documents: ApplicantDocument[]
  canEdit: boolean
  canSubmit: boolean
  canReplaceRejectedDocuments: boolean
  resultsVisible: boolean
  canConfirmReregistration: boolean
  reregistrationConfirmed: boolean
}

export interface MyAdmission {
  period: PublicAdmissionPeriod | null
  application: MyApplication | null
}

export interface AdmissionPeriod {
  id: string
  name: string
  academicYearId: string
  academicYearName: string
  opensAt: string
  closesAt: string
  resultsAnnouncedAt: string
  reregistrationDeadline: string
  applicantCount: number
}

export interface PeriodInput {
  academicYearId: string
  name: string
  opensAt: string
  closesAt: string
  resultsAnnouncedAt: string
  reregistrationDeadline: string
}

export interface ApplicantListItem {
  id: string
  fullName: string
  email: string
  status: ApplicantStatus
  verifiedDocuments: number
  totalDocuments: number
  submittedAt: string | null
  periodName: string
}

export interface ApplicantDetail {
  id: string
  email: string
  status: ApplicantStatus
  form: ApplicantForm
  notes: string | null
  submittedAt: string | null
  decidedAt: string | null
  reregistrationConfirmedAt: string | null
  studentId: string | null
  periodName: string
  documents: ApplicantDocument[]
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ApplicantsParams {
  status?: ApplicantStatus
  search?: string
  page: number
  pageSize: number
}

interface UploadUrl {
  url: string
  key: string
  contentType: string
  expiresAt: string
}

interface DownloadUrl {
  url: string
  expiresAt: string
}

export const admissionKeys = {
  all: ['admissions'] as const,
  open: () => [...admissionKeys.all, 'open'] as const,
  mine: () => [...admissionKeys.all, 'mine'] as const,
  periods: () => [...admissionKeys.all, 'periods'] as const,
  applicants: (params: ApplicantsParams) => [...admissionKeys.all, 'applicants', params] as const,
  applicant: (id: string) => [...admissionKeys.all, 'applicant', id] as const,
  years: () => [...admissionKeys.all, 'academic-years'] as const,
}

const base = '/api/v1/admissions'

// ---- the public banner ----

/** The period that is open now, or else the next one; null when the school has none to show. Anybody may ask. */
export function useOpenAdmission() {
  return useQuery({
    queryKey: admissionKeys.open(),
    queryFn: async () => (await customInstance<{ period: PublicAdmissionPeriod | null }>({ url: `${base}/open`, method: 'GET' })).period,
    staleTime: 60_000,
    retry: false,
  })
}

// ---- the applicant ----

export function useMyAdmission() {
  return useQuery({
    queryKey: admissionKeys.mine(),
    queryFn: () => customInstance<MyAdmission>({ url: `${base}/me`, method: 'GET' }),
  })
}

/** Every step of the application answers with the whole application: the page keeps it in the cache, no second request. */
function useApplicationMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<MyApplication>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: (application) =>
      queryClient.setQueryData<MyAdmission>(admissionKeys.mine(), (current) => (current ? { ...current, application } : { period: null, application })),
  })
}

export function useStartApplication() {
  return useApplicationMutation(() => customInstance<MyApplication>({ url: `${base}/me`, method: 'POST' }))
}

export function useSaveApplicantForm() {
  return useApplicationMutation((form: ApplicantForm) => customInstance<MyApplication>({ url: `${base}/me/form`, method: 'PUT', data: { form } }))
}

/**
 * Three steps for one document: ask the API for a presigned address, send the file straight to storage, and attach it
 * to the application with its key. The answer is the whole application.
 */
export function useUploadApplicantDocument() {
  return useApplicationMutation(async ({ type, file }: { type: DocumentType; file: File }) => {
    const issued = await customInstance<UploadUrl>({
      url: `${base}/me/documents/upload-url`,
      method: 'POST',
      data: { type, contentType: file.type, sizeBytes: file.size },
    })
    await putToPresignedUrl(issued.url, file, issued.contentType)
    return customInstance<MyApplication>({ url: `${base}/me/documents`, method: 'PUT', data: { type, fileKey: issued.key, fileName: file.name } })
  })
}

export function useSubmitApplication() {
  return useApplicationMutation(() => customInstance<MyApplication>({ url: `${base}/me/submit`, method: 'POST' }))
}

export function useConfirmReregistration() {
  return useApplicationMutation(() => customInstance<MyApplication>({ url: `${base}/me/reregistration`, method: 'POST' }))
}

// ---- the admin staff: the applications ----

export function useApplicants(params: ApplicantsParams) {
  return useQuery({
    queryKey: admissionKeys.applicants(params),
    queryFn: () =>
      customInstance<Paged<ApplicantListItem>>({
        url: `${base}/applicants`,
        method: 'GET',
        params: { Status: params.status, Search: params.search, Page: params.page, PageSize: params.pageSize },
      }),
    placeholderData: keepPreviousData,
  })
}

export function useApplicant(id: string) {
  return useQuery({
    queryKey: admissionKeys.applicant(id),
    queryFn: () => customInstance<ApplicantDetail>({ url: `${base}/applicants/${id}`, method: 'GET' }),
  })
}

/** Opens the signed address of a document in a new tab. */
export function useOpenApplicantDocument() {
  return useMutation({
    mutationFn: async ({ id, type }: { id: string; type: DocumentType }) => {
      const { url } = await customInstance<DownloadUrl>({ url: `${base}/applicants/${id}/documents/${type}/url`, method: 'GET' })
      openInNewTab(url)
    },
  })
}

/** Review, decision and enrollment all answer with the application as the staff see it. */
function useApplicantMutation<TVariables extends { id: string }>(mutationFn: (variables: TVariables) => Promise<ApplicantDetail>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: (detail, variables) => {
      queryClient.setQueryData(admissionKeys.applicant(variables.id), detail)
      return queryClient.invalidateQueries({ queryKey: [...admissionKeys.all, 'applicants'] })
    },
  })
}

export function useReviewDocument() {
  return useApplicantMutation(({ id, type, verified, note }: { id: string; type: DocumentType; verified: boolean; note?: string }) =>
    customInstance<ApplicantDetail>({
      url: `${base}/applicants/${id}/documents/${type}/review`,
      method: 'PUT',
      data: { applicantId: id, type, verified, note: note ?? null },
    }),
  )
}

export function useDecideApplicant() {
  return useApplicantMutation(({ id, accept, note }: { id: string; accept: boolean; note?: string }) =>
    customInstance<ApplicantDetail>({ url: `${base}/applicants/${id}/decision`, method: 'POST', data: { applicantId: id, accept, note: note ?? null } }),
  )
}

export function useEnrollApplicant() {
  return useApplicantMutation(({ id, classroomId, nisn }: { id: string; classroomId: string; nisn: string }) =>
    customInstance<ApplicantDetail>({ url: `${base}/applicants/${id}/enroll`, method: 'POST', data: { applicantId: id, classroomId, nisn } }),
  )
}

// ---- the admin staff: the periods ----

export function useAdmissionPeriods() {
  return useQuery({
    queryKey: admissionKeys.periods(),
    queryFn: () => customInstance<AdmissionPeriod[]>({ url: `${base}/periods`, method: 'GET' }),
  })
}

function usePeriodMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: admissionKeys.all }),
  })
}

export function useCreatePeriod() {
  return usePeriodMutation((input: PeriodInput) => customInstance<AdmissionPeriod>({ url: `${base}/periods`, method: 'POST', data: input }))
}

export function useUpdatePeriod() {
  return usePeriodMutation(({ id, input }: { id: string; input: Omit<PeriodInput, 'academicYearId'> }) =>
    customInstance<AdmissionPeriod>({ url: `${base}/periods/${id}`, method: 'PUT', data: { id, ...input } }),
  )
}

export function useDeletePeriod() {
  return usePeriodMutation((id: string) => customInstance<void>({ url: `${base}/periods/${id}`, method: 'DELETE' }))
}

/** The academic years, for the picker of a new period. */
export function useAcademicYearOptions() {
  return useQuery({
    queryKey: admissionKeys.years(),
    queryFn: async () => (await listAcademicYears({ Page: 1, PageSize: 100 })).items.map((year) => ({ id: year.id, name: year.name, isActive: year.isActive })),
    staleTime: 5 * 60_000,
  })
}
