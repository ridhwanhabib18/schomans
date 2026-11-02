import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import type { ApplicantDetail, ApplicantListItem, DocumentType } from '../api/admissions'
import { ApplicantDetailPage } from './ApplicantDetailPage'
import { ApplicantsPage } from './ApplicantsPage'

const api = vi.hoisted(() => ({ listAcademicYears: vi.fn(), listClassrooms: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const open = vi.hoisted(() => ({ tab: vi.fn() }))
vi.mock('@/shared/lib/download', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/download')>()), openInNewTab: open.tab }))

type Config = { url: string; method: string; params?: Record<string, unknown>; data?: Record<string, unknown> }
type Handler = (config: Config) => unknown

const item = (overrides: Partial<ApplicantListItem> = {}): ApplicantListItem => ({
  id: 'a1',
  fullName: 'Dewi Lestari',
  email: 'dewi@example.test',
  status: 'InVerification',
  verifiedDocuments: 2,
  totalDocuments: 3,
  submittedAt: '2026-10-20T03:00:00Z',
  periodName: 'Admissions 2027/2028',
  ...overrides,
})

const doc = (type: DocumentType, status: 'Pending' | 'Verified' | 'Rejected' = 'Pending', reviewNote: string | null = null) => ({ type, fileName: `${type}.pdf`, status, reviewNote })

const detail = (overrides: Partial<ApplicantDetail> = {}): ApplicantDetail => ({
  id: 'a1',
  email: 'dewi@example.test',
  status: 'InVerification',
  form: {
    fullName: 'Dewi Lestari',
    dateOfBirth: '2013-05-17',
    placeOfBirth: 'Bandung',
    address: 'Jl. Merdeka 1',
    guardianName: 'Ibu Lestari',
    guardianPhone: '0812345678',
    previousSchool: 'SD Negeri 1',
  },
  notes: null,
  submittedAt: '2026-10-20T03:00:00Z',
  decidedAt: null,
  reregistrationConfirmedAt: null,
  studentId: null,
  periodName: 'Admissions 2027/2028',
  documents: [doc('BirthCertificate'), doc('FamilyCard'), doc('ReportCard')],
  ...overrides,
})

/** The API as the staff pages see it: one handler per `METHOD path` (the admissions prefix left out). */
function serve(handlers: Record<string, Handler>) {
  http.customInstance.mockImplementation(async (config: Config) => {
    const key = `${config.method} ${config.url.replace('/api/v1/admissions', '')}`
    const handler = handlers[key]
    if (!handler) throw new Error(`unexpected request ${key}`)
    return handler(config)
  })
}

const calls = (key: string) =>
  http.customInstance.mock.calls.map(([config]) => config as Config).filter((config) => `${config.method} ${config.url.replace('/api/v1/admissions', '')}` === key)

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)
const renderDetail = () => renderRoutes([{ path: '/manage/admissions/:id', element: <ApplicantDetailPage /> }], '/manage/admissions/a1')

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  http.customInstance.mockReset()
  open.tab.mockReset()
  useToastStore.setState({ toasts: [] })
  api.listAcademicYears.mockResolvedValue({ items: [{ id: 'y1', name: '2026/2027', isActive: true }] })
  api.listClassrooms.mockResolvedValue({ items: [{ id: 'c1', name: 'Class A' }, { id: 'c2', name: 'Class B' }] })
  signInAs('Admin', 'Sari Utami')
})

describe('ApplicantsPage', () => {
  it('lists the applications with their status and how many documents are verified', async () => {
    serve({ 'GET /applicants': () => pageOf([item(), item({ id: 'a2', fullName: 'Budi Hartono', email: 'budi@example.test', status: 'Submitted', verifiedDocuments: 0, submittedAt: null })]) })
    renderRoutes([{ path: '*', element: <ApplicantsPage /> }], '/')

    expect(await screen.findByRole('cell', { name: 'Dewi Lestari' })).toBeTruthy()
    expect(screen.getByText('2 of 3')).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'In verification' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'Handed in' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Open: Dewi Lestari' }).getAttribute('href')).toBe('/manage/admissions/a1')
    expect(calls('GET /applicants')[0]?.params).toMatchObject({ Page: 1, PageSize: 20 })
  })

  it('asks for the chosen status only', async () => {
    serve({ 'GET /applicants': () => pageOf([item({ status: 'Accepted' })]) })
    renderRoutes([{ path: '*', element: <ApplicantsPage /> }], '/')
    await screen.findByRole('cell', { name: 'Dewi Lestari' })

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Accepted' } })

    await waitFor(() => expect(calls('GET /applicants').some((c) => c.params?.Status === 'Accepted')).toBe(true))
  })

  it('shows an empty state and an error with a retry', async () => {
    serve({ 'GET /applicants': () => pageOf<ApplicantListItem>([]) })
    renderRoutes([{ path: '*', element: <ApplicantsPage /> }], '/')
    expect(await screen.findByText('No applications')).toBeTruthy()
  })

  it('shows an error when the list cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    renderRoutes([{ path: '*', element: <ApplicantsPage /> }], '/')

    expect((await screen.findByRole('alert')).textContent).toContain('The applications could not be loaded.')
  })
})

describe('ApplicantDetailPage', () => {
  it('shows the form and the documents of the application', async () => {
    serve({ 'GET /applicants/a1': () => detail({ documents: [doc('BirthCertificate', 'Verified'), doc('FamilyCard'), doc('ReportCard', 'Rejected', 'Blurred')] }) })
    renderDetail()

    expect(await screen.findByRole('heading', { level: 1, name: 'Dewi Lestari' })).toBeTruthy()
    expect(screen.getByText('Jl. Merdeka 1')).toBeTruthy()
    expect(screen.getByText('17/05/2013')).toBeTruthy()
    expect(screen.getByText('Verified')).toBeTruthy()
    expect(screen.getByText('Note from the staff: Blurred')).toBeTruthy()
  })

  it('opens the signed address of a document in a new tab', async () => {
    serve({
      'GET /applicants/a1': () => detail(),
      'GET /applicants/a1/documents/FamilyCard/url': () => ({ url: 'https://storage.test/card.pdf?sig=1', expiresAt: '2026-10-29T05:00:00Z' }),
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Open the file: Family card' }))

    await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/card.pdf?sig=1'))
  })

  it('verifies a document', async () => {
    serve({
      'GET /applicants/a1': () => detail(),
      'PUT /applicants/a1/documents/BirthCertificate/review': () => detail({ documents: [doc('BirthCertificate', 'Verified'), doc('FamilyCard'), doc('ReportCard')] }),
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Verify: Birth certificate' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Document verified']))
    expect(calls('PUT /applicants/a1/documents/BirthCertificate/review')[0]?.data).toMatchObject({ verified: true })
    expect((await screen.findByRole('button', { name: 'Verify: Birth certificate' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('sends a document back only with a note for the applicant', async () => {
    serve({
      'GET /applicants/a1': () => detail(),
      'PUT /applicants/a1/documents/ReportCard/review': () => detail({ documents: [doc('BirthCertificate'), doc('FamilyCard'), doc('ReportCard', 'Rejected', 'Page 2 is missing')] }),
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Reject: Latest report card' }))
    const dialog = await screen.findByRole('dialog')
    const send = within(dialog).getByRole('button', { name: 'Send back' }) as HTMLButtonElement
    expect(send.disabled).toBe(true)
    fireEvent.change(within(dialog).getByLabelText(/What is wrong with the document/), { target: { value: 'Page 2 is missing' } })
    expect(send.disabled).toBe(false)
    fireEvent.click(send)

    await waitFor(() => expect(toasts()).toEqual(['success:Document sent back']))
    expect(calls('PUT /applicants/a1/documents/ReportCard/review')[0]?.data).toMatchObject({ verified: false, note: 'Page 2 is missing' })
    expect(await screen.findByText('Note from the staff: Page 2 is missing')).toBeTruthy()
  })

  it('shows the answer of the API when accepting is refused', async () => {
    serve({
      'GET /applicants/a1': () => detail(),
      'POST /applicants/a1/decision': () => {
        throw apiError(409, { code: 'admissions.documents_not_verified' })
      },
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Accept' }))

    expect((await screen.findByRole('alert')).textContent).toContain('Verify all three documents before accepting.')
    expect(screen.queryByRole('alertdialog')).toBeNull()
  })

  it('accepts an applicant whose documents are verified', async () => {
    serve({
      'GET /applicants/a1': () => detail({ documents: [doc('BirthCertificate', 'Verified'), doc('FamilyCard', 'Verified'), doc('ReportCard', 'Verified')] }),
      'POST /applicants/a1/decision': () => detail({ status: 'Accepted', decidedAt: '2026-10-25T03:00:00Z' }),
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Decision saved']))
    expect(calls('POST /applicants/a1/decision')[0]?.data).toMatchObject({ accept: true })
    expect(await screen.findByText('Accepted')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull()
  })

  it('rejects an application only with the reason the applicant will read', async () => {
    serve({
      'GET /applicants/a1': () => detail(),
      'POST /applicants/a1/decision': () => detail({ status: 'Rejected', notes: 'No places left', decidedAt: '2026-10-25T03:00:00Z' }),
    })
    renderDetail()

    fireEvent.click(await screen.findByRole('button', { name: 'Reject application' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Reject application' }) as HTMLButtonElement
    expect(confirm.disabled).toBe(true)
    fireEvent.change(within(dialog).getByLabelText(/Note for the applicant/), { target: { value: 'No places left' } })
    fireEvent.click(confirm)

    await waitFor(() => expect(toasts()).toEqual(['success:Decision saved']))
    expect(calls('POST /applicants/a1/decision')[0]?.data).toMatchObject({ accept: false, note: 'No places left' })
    expect(await screen.findByText('No places left')).toBeTruthy()
  })

  describe('enrollment', () => {
    const confirmed = () => detail({ status: 'Accepted', reregistrationConfirmedAt: '2026-12-20T03:00:00Z', documents: [doc('BirthCertificate', 'Verified'), doc('FamilyCard', 'Verified'), doc('ReportCard', 'Verified')] })

    it('waits for the applicant to confirm the place before it offers the enrollment', async () => {
      serve({ 'GET /applicants/a1': () => detail({ status: 'Accepted' }) })
      renderDetail()

      expect(await screen.findByText('The applicant has not confirmed the place yet.')).toBeTruthy()
      expect(screen.queryByRole('button', { name: 'Enroll' })).toBeNull()
    })

    it('needs a classroom and a NISN of exactly 10 digits', async () => {
      serve({ 'GET /applicants/a1': confirmed })
      renderDetail()

      fireEvent.change(await screen.findByLabelText(/^NISN/), { target: { value: '12345' } })
      fireEvent.change(screen.getByLabelText(/^Classroom/), { target: { value: 'c1' } })
      fireEvent.click(screen.getByRole('button', { name: 'Enroll' }))

      expect(await screen.findByText('The NISN must be exactly 10 digits.')).toBeTruthy()
      expect(calls('POST /applicants/a1/enroll')).toHaveLength(0)
    })

    it('enrolls the applicant in the chosen classroom', async () => {
      serve({
        'GET /applicants/a1': confirmed,
        'POST /applicants/a1/enroll': () => detail({ status: 'Enrolled', studentId: 's1', reregistrationConfirmedAt: '2026-12-20T03:00:00Z' }),
      })
      renderDetail()

      const classroom = await screen.findByLabelText(/^Classroom/)
      await waitFor(() => expect(within(classroom as HTMLElement).getByRole('option', { name: 'Class A' })).toBeTruthy())
      fireEvent.change(classroom, { target: { value: 'c1' } })
      fireEvent.change(screen.getByLabelText(/^NISN/), { target: { value: '1234567890' } })
      fireEvent.click(screen.getByRole('button', { name: 'Enroll' }))

      await waitFor(() => expect(toasts()).toEqual(['success:Applicant enrolled as a student']))
      expect(calls('POST /applicants/a1/enroll')[0]?.data).toMatchObject({ classroomId: 'c1', nisn: '1234567890' })
      expect(await screen.findByText('Enrolled as a student.')).toBeTruthy()
    })

    it('shows a failing identity provider inside the enrollment form', async () => {
      serve({
        'GET /applicants/a1': confirmed,
        'POST /applicants/a1/enroll': () => {
          throw apiError(502, { code: 'admissions.identity_failed' })
        },
      })
      renderDetail()

      const classroom = await screen.findByLabelText(/^Classroom/)
      await waitFor(() => expect(within(classroom as HTMLElement).getByRole('option', { name: 'Class A' })).toBeTruthy())
      fireEvent.change(classroom, { target: { value: 'c1' } })
      fireEvent.change(screen.getByLabelText(/^NISN/), { target: { value: '1234567890' } })
      fireEvent.click(screen.getByRole('button', { name: 'Enroll' }))

      const section = screen.getByRole('heading', { name: 'Enroll as a student' }).closest('section') as HTMLElement
      const alert = await within(section).findByRole('alert')
      expect(alert.textContent).toContain('could not be changed to a student in the identity provider')
      expect((screen.getByRole('button', { name: 'Enroll' }) as HTMLButtonElement).disabled).toBe(false)
    })

    it('shows a NISN that is taken', async () => {
      serve({
        'GET /applicants/a1': confirmed,
        'POST /applicants/a1/enroll': () => {
          throw apiError(409, { code: 'students.nisn_taken' })
        },
      })
      renderDetail()

      const classroom = await screen.findByLabelText(/^Classroom/)
      await waitFor(() => expect(within(classroom as HTMLElement).getByRole('option', { name: 'Class B' })).toBeTruthy())
      fireEvent.change(classroom, { target: { value: 'c2' } })
      fireEvent.change(screen.getByLabelText(/^NISN/), { target: { value: '1234567890' } })
      fireEvent.click(screen.getByRole('button', { name: 'Enroll' }))

      expect((await screen.findByRole('alert')).textContent).toContain('Another student already has this NISN.')
    })
  })

  it('shows an error when the application cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(404))
    renderDetail()

    expect((await screen.findByRole('alert')).textContent).toContain('The application could not be loaded.')
  })
})
