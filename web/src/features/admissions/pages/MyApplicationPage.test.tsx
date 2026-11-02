import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, renderPage, signInAs } from '@/test/utils'
import type { ApplicantDocument, ApplicantForm, DocumentType, MyAdmission, MyApplication, PublicAdmissionPeriod } from '../api/admissions'
import { MyApplicationPage } from './MyApplicationPage'

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

const upload = vi.hoisted(() => ({ put: vi.fn() }))
vi.mock('@/shared/lib/upload', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/upload')>()), putToPresignedUrl: upload.put }))

const period: PublicAdmissionPeriod = {
  id: 'p1',
  name: 'Admissions 2027/2028',
  opensAt: '2026-10-01T00:00:00Z',
  closesAt: '2026-12-01T00:00:00Z',
  resultsAnnouncedAt: '2026-12-15T03:00:00Z',
  reregistrationDeadline: '2027-01-05T03:00:00Z',
  isOpen: true,
}

const form: ApplicantForm = {
  fullName: 'Dewi Lestari',
  dateOfBirth: '2013-05-17',
  placeOfBirth: 'Bandung',
  address: 'Jl. Merdeka 1',
  guardianName: 'Ibu Lestari',
  guardianPhone: '0812345678',
  previousSchool: 'SD Negeri 1',
}

const doc = (type: DocumentType, status: ApplicantDocument['status'] = 'Pending', reviewNote: string | null = null): ApplicantDocument => ({
  type,
  fileName: `${type}.pdf`,
  status,
  reviewNote,
})

const allDocs = (status: ApplicantDocument['status'] = 'Pending') => [doc('BirthCertificate', status), doc('FamilyCard', status), doc('ReportCard', status)]

const application = (overrides: Partial<MyApplication> = {}): MyApplication => ({
  id: 'a1',
  status: 'Draft',
  form,
  notes: null,
  submittedAt: null,
  documents: [],
  canEdit: true,
  canSubmit: false,
  canReplaceRejectedDocuments: false,
  resultsVisible: false,
  canConfirmReregistration: false,
  reregistrationConfirmed: false,
  ...overrides,
})

type Handler = (config: { url: string; method: string; data?: unknown }) => unknown

/** The API as the applicant's page sees it: one handler per `METHOD path`. */
function serve(mine: MyAdmission, handlers: Record<string, Handler> = {}) {
  http.customInstance.mockImplementation(async (config: { url: string; method: string; data?: unknown }) => {
    const key = `${config.method} ${config.url.replace('/api/v1/admissions', '')}`
    if (key === 'GET /me') return mine
    const handler = handlers[key]
    if (!handler) throw new Error(`unexpected request ${key}`)
    return handler(config)
  })
}

const calls = (key: string) =>
  http.customInstance.mock.calls
    .map(([config]) => config as { url: string; method: string; data?: unknown })
    .filter((config) => `${config.method} ${config.url.replace('/api/v1/admissions', '')}` === key)

const pdf = (name: string, size = 2048) => new File([new Uint8Array(size)], name, { type: 'application/pdf' })
const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  http.customInstance.mockReset()
  upload.put.mockReset().mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
  signInAs('Applicant', 'Dewi Lestari')
})

describe('MyApplicationPage', () => {
  it('says there is nothing to apply for when no period is open and there is no application', async () => {
    serve({ period: null, application: null })
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('No admissions are open')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Start application' })).toBeNull()
  })

  it('starts the application in the open period and then shows the form', async () => {
    serve({ period, application: null }, { 'POST /me': () => application() })
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('Admissions 2027/2028')).toBeTruthy()
    expect(screen.getByText('Applications close')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Start application' }))

    expect(await screen.findByLabelText(/^Full name/)).toBeTruthy()
    expect(calls('POST /me')).toHaveLength(1)
  })

  it('shows the start error of the API in words', async () => {
    serve(
      { period, application: null },
      {
        'POST /me': () => {
          throw apiError(409, { code: 'admissions.not_open' })
        },
      },
    )
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Start application' }))

    expect((await screen.findByRole('alert')).textContent).toContain('There is no admission period open at the moment.')
  })

  it('saves the form as a draft, and only offers saving when something changed', async () => {
    serve(
      { period, application: application() },
      { 'PUT /me/form': (config) => application({ form: (config.data as { form: ApplicantForm }).form }) },
    )
    renderPage(<MyApplicationPage />)

    const save = (await screen.findByRole('button', { name: 'Save draft' })) as HTMLButtonElement
    expect(save.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText(/^Address/), { target: { value: 'Jl. Baru 2' } })
    expect(save.disabled).toBe(false)
    fireEvent.click(save)

    await waitFor(() => expect(toasts()).toEqual(['success:Draft saved']))
    expect((calls('PUT /me/form')[0]?.data as { form: ApplicantForm }).form.address).toBe('Jl. Baru 2')
    await waitFor(() => expect((screen.getByRole('button', { name: 'Save draft' }) as HTMLButtonElement).disabled).toBe(true))
  })

  it('uploads a document straight to storage and attaches it with its key', async () => {
    serve(
      { period, application: application() },
      {
        'POST /me/documents/upload-url': () => ({ url: 'https://storage.test/put', key: 'admissions/a1/BirthCertificate/x.pdf', contentType: 'application/pdf', expiresAt: '2026-10-29T05:00:00Z' }),
        'PUT /me/documents': () => application({ documents: [doc('BirthCertificate')] }),
      },
    )
    renderPage(<MyApplicationPage />)

    fireEvent.change(await screen.findByLabelText('Birth certificate: choose a file'), { target: { files: [pdf('akta.pdf', 4096)] } })

    await waitFor(() => expect(toasts()).toEqual(['success:Document uploaded']))
    expect(calls('POST /me/documents/upload-url')[0]?.data).toEqual({ type: 'BirthCertificate', contentType: 'application/pdf', sizeBytes: 4096 })
    expect(upload.put).toHaveBeenCalledWith('https://storage.test/put', expect.any(File), 'application/pdf')
    expect(calls('PUT /me/documents')[0]?.data).toEqual({ type: 'BirthCertificate', fileKey: 'admissions/a1/BirthCertificate/x.pdf', fileName: 'akta.pdf' })
    expect(await screen.findByText('BirthCertificate.pdf')).toBeTruthy()
  })

  it('refuses a wrong file type or a file over 10 MB before anything is sent', async () => {
    serve({ period, application: application() })
    renderPage(<MyApplicationPage />)
    const picker = await screen.findByLabelText('Family card: choose a file')

    fireEvent.change(picker, { target: { files: [new File(['x'], 'card.docx', { type: 'application/msword' })] } })
    expect((await screen.findByRole('alert')).textContent).toContain('Upload a PDF or an image (JPEG, PNG, WebP).')

    fireEvent.change(picker, { target: { files: [pdf('big.pdf', 10 * 1024 * 1024 + 1)] } })
    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('at most 10 MB'))

    expect(calls('POST /me/documents/upload-url')).toHaveLength(0)
    expect(upload.put).not.toHaveBeenCalled()
  })

  it('explains why the application cannot be handed in yet', async () => {
    serve({ period, application: application({ documents: [doc('BirthCertificate')] }) })
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('Upload all three documents first.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Hand in the application' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('hands the application in after a confirmation and then shows it as handed in', async () => {
    serve(
      { period, application: application({ documents: allDocs(), canSubmit: true }) },
      { 'POST /me/submit': () => application({ status: 'Submitted', documents: allDocs(), canEdit: false, canSubmit: false, submittedAt: '2026-10-29T03:00:00Z' }) },
    )
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Hand in the application' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(calls('POST /me/submit')).toHaveLength(0)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Hand in' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Application handed in']))
    expect(calls('POST /me/submit')).toHaveLength(1)
    expect((await screen.findByText('Your application is handed in. The admin staff will look at it soon.')).textContent).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Hand in the application' })).toBeNull()
  })

  it('tracks the stage and keeps a decision hidden until the results are announced', async () => {
    // The API already answers "in verification" before the announcement; the page shows what it gets.
    serve({ period, application: application({ status: 'InVerification', documents: allDocs('Verified'), canEdit: false, resultsVisible: false }) })
    renderPage(<MyApplicationPage />)

    const tracker = await screen.findByRole('region', { name: 'Where your application stands' })
    expect(within(tracker).getByText('In verification').closest('li')?.getAttribute('aria-current')).toBe('step')
    expect(within(tracker).getByText('Accepted')).toBeTruthy()
    expect(screen.queryByText(/Message from the school/)).toBeNull()
    expect(screen.queryByRole('button', { name: 'Confirm my place' })).toBeNull()
  })

  it('shows the staff note with a rejection and the rejected stage', async () => {
    serve({ period, application: application({ status: 'Rejected', notes: 'No places left', documents: allDocs('Verified'), canEdit: false, resultsVisible: true }) })
    renderPage(<MyApplicationPage />)

    const tracker = await screen.findByRole('region', { name: 'Where your application stands' })
    expect(within(tracker).getByText('Rejected').closest('li')?.getAttribute('aria-current')).toBe('step')
    expect(screen.getByText('Message from the school: No places left')).toBeTruthy()
  })

  it('lets the applicant upload a document the staff sent back, and no other', async () => {
    serve({
      period,
      application: application({
        status: 'InVerification',
        canEdit: false,
        canReplaceRejectedDocuments: true,
        documents: [doc('BirthCertificate', 'Rejected', 'The scan is blurred'), doc('FamilyCard', 'Verified'), doc('ReportCard', 'Pending')],
      }),
    })
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('Note from the staff: The scan is blurred')).toBeTruthy()
    expect(screen.getByLabelText('Birth certificate: choose a file')).toBeTruthy()
    expect(screen.queryByLabelText('Family card: choose a file')).toBeNull()
    expect(screen.queryByLabelText('Latest report card: choose a file')).toBeNull()
    expect(screen.getByText('Needs a new upload')).toBeTruthy()
  })

  it('lets an accepted applicant confirm the place once, before the deadline', async () => {
    serve(
      { period, application: application({ status: 'Accepted', canEdit: false, documents: allDocs('Verified'), resultsVisible: true, canConfirmReregistration: true }) },
      { 'POST /me/reregistration': () => application({ status: 'Accepted', canEdit: false, documents: allDocs('Verified'), resultsVisible: true, reregistrationConfirmed: true }) },
    )
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Confirm my place' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Your place is confirmed']))
    expect(await screen.findByText('You confirmed your place. The admin staff will enroll you as a student.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Confirm my place' })).toBeNull()
  })

  it('shows an error with a retry when the application cannot be loaded', async () => {
    http.customInstance.mockRejectedValue(apiError(500))
    renderPage(<MyApplicationPage />)

    expect((await screen.findByRole('alert')).textContent).toContain('Your application could not be loaded.')
  })
})
