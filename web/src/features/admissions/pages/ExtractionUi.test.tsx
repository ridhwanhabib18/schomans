import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, renderPage, renderRoutes, signInAs } from '@/test/utils'
import type { ApplicantDetail, ApplicantExtractions, ApplicantForm, MyApplication } from '../api/admissions'
import { suggestionsFor } from '../extraction'
import { ApplicantDetailPage } from './ApplicantDetailPage'
import { MyApplicationPage } from './MyApplicationPage'

const api = vi.hoisted(() => ({ listAcademicYears: vi.fn(), listClassrooms: vi.fn() }))
vi.mock('@/api/generated/schomans', () => api)

const http = vi.hoisted(() => ({ customInstance: vi.fn() }))
vi.mock('@/shared/lib/http', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/http')>()), customInstance: http.customInstance }))

type Config = { url: string; method: string; data?: unknown }

const form: ApplicantForm = {
  fullName: 'Rina Putri',
  dateOfBirth: '2013-05-17',
  placeOfBirth: 'Bandung',
  address: '',
  guardianName: 'Agus Putra',
  guardianPhone: '0812345678',
  previousSchool: 'SD Negeri 1',
}

const fields = (overrides: Partial<ApplicantExtractions['documents'][number]['fields']> = {}) => ({
  fullName: null,
  nik: null,
  dateOfBirth: null,
  placeOfBirth: null,
  address: null,
  fatherName: null,
  motherName: null,
  guardianName: null,
  grades: [],
  ...overrides,
})

const extractions = (overrides: Partial<ApplicantExtractions> = {}): ApplicantExtractions => ({
  documents: [
    { type: 'BirthCertificate', extractedAt: '2026-11-03T03:00:00Z', fields: fields({ fullName: 'Rina Putri Lestari', dateOfBirth: '2014-05-05', placeOfBirth: 'Bandung' }) },
    { type: 'FamilyCard', extractedAt: '2026-11-03T03:00:00Z', fields: fields({ fullName: 'Rina Putri Lestari', nik: '3201014505140001', address: 'Jl. Merdeka No. 10, Bandung', guardianName: 'Budi Santoso' }) },
  ],
  mismatches: [],
  ...overrides,
})

const application = (): MyApplication => ({
  id: 'a1',
  status: 'Draft',
  form,
  notes: null,
  submittedAt: null,
  documents: [
    { type: 'BirthCertificate', fileName: 'akta.png', status: 'Pending', reviewNote: null },
    { type: 'FamilyCard', fileName: 'kk.jpg', status: 'Pending', reviewNote: null },
    { type: 'ReportCard', fileName: 'rapor.pdf', status: 'Pending', reviewNote: null },
  ],
  canEdit: true,
  canSubmit: false,
  canReplaceRejectedDocuments: false,
  resultsVisible: false,
  canConfirmReregistration: false,
  reregistrationConfirmed: false,
})

const calls = (method: string, url: string) => http.customInstance.mock.calls.map(([c]) => c as Config).filter((c) => c.method === method && c.url === url)

function serveApplicant(read: ApplicantExtractions = extractions(), overrides: Record<string, () => unknown> = {}) {
  http.customInstance.mockImplementation(async ({ url, method }: Config) => {
    const key = `${method} ${url}`
    if (overrides[key]) return overrides[key]()
    if (key === 'GET /api/v1/admissions/me') return { period: null, application: application() }
    if (key === 'GET /api/v1/admissions/me/extractions') return read
    if (key.startsWith('POST /api/v1/admissions/me/documents/') && key.endsWith('/extract')) return read.documents[0]
    if (key === 'PUT /api/v1/admissions/me/form') return application()
    throw new Error(`unexpected request ${key}`)
  })
}

beforeEach(() => {
  http.customInstance.mockReset()
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listAcademicYears.mockResolvedValue({ items: [] })
  api.listClassrooms.mockResolvedValue({ items: [] })
  useToastStore.setState({ toasts: [] })
})

describe('the applicant and the AI reading of the documents', () => {
  beforeEach(() => signInAs('Applicant', 'Rina Putri'))

  it('offers to read a photo with the AI but not a PDF', async () => {
    serveApplicant()
    renderPage(<MyApplicationPage />)

    expect(await screen.findByRole('button', { name: /Read with AI.*Birth certificate/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /Read with AI.*Family card/ })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Read with AI.*Latest report card/ })).toBeNull()
    expect(screen.getByText(/reads photos only/)).toBeTruthy()
  })

  it('reads a document and says so', async () => {
    serveApplicant()
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Read with AI.*Family card/ }))

    await waitFor(() => expect(calls('POST', '/api/v1/admissions/me/documents/FamilyCard/extract')).toHaveLength(1))
    await waitFor(() => expect(useToastStore.getState().toasts.map((x) => x.title)).toContain('The AI read the document. Check what it found.'))
  })

  it('shows why the AI could not read a document', async () => {
    serveApplicant(extractions(), { 'POST /api/v1/admissions/me/documents/FamilyCard/extract': () => { throw apiError(400, { code: 'admissions.extract_too_large' }) } })
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: /Read with AI.*Family card/ }))

    expect(await screen.findByText('The photo is larger than 5 MB. Upload a smaller photo to use the AI.')).toBeTruthy()
  })

  it('suggests what the documents say, and fills a field only when the applicant chooses it', async () => {
    serveApplicant()
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('Suggestions from your documents')).toBeTruthy()
    // the name comes from the birth certificate first; the address is empty on the form, the guardian differs
    expect(screen.getByText('Rina Putri Lestari')).toBeTruthy()
    expect(screen.getByText('Jl. Merdeka No. 10, Bandung')).toBeTruthy()
    expect((screen.getByLabelText(/Full name/) as HTMLInputElement).value).toBe('Rina Putri')

    fireEvent.click(screen.getByRole('button', { name: /Use.*Address/ }))

    expect((screen.getByLabelText(/Address/) as HTMLInputElement).value).toBe('Jl. Merdeka No. 10, Bandung')
    expect((screen.getByLabelText(/Full name/) as HTMLInputElement).value).toBe('Rina Putri')
    expect(screen.getByText('Filled in from a document: check it, then save')).toBeTruthy()
    expect(calls('PUT', '/api/v1/admissions/me/form')).toHaveLength(0)
  })

  it('uses every suggestion at once and saves only when the applicant saves', async () => {
    serveApplicant()
    renderPage(<MyApplicationPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Use all suggestions' }))

    expect((screen.getByLabelText(/Full name/) as HTMLInputElement).value).toBe('Rina Putri Lestari')
    expect((screen.getByLabelText(/Date of birth/) as HTMLInputElement).value).toBe('2014-05-05')
    expect((screen.getByLabelText(/Parent or guardian/) as HTMLInputElement).value).toBe('Budi Santoso')
    expect(screen.getAllByText('Filled in from a document: check it, then save')).toHaveLength(4)

    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(calls('PUT', '/api/v1/admissions/me/form')).toHaveLength(1))
    expect((calls('PUT', '/api/v1/admissions/me/form')[0]?.data as { form: ApplicantForm }).form.fullName).toBe('Rina Putri Lestari')
    await waitFor(() => expect(screen.queryByText('Filled in from a document: check it, then save')).toBeNull())
  })

  it('says the form already matches when there is nothing to suggest', async () => {
    serveApplicant(extractions({ documents: [{ type: 'FamilyCard', extractedAt: '2026-11-03T03:00:00Z', fields: fields({ fullName: 'rina putri', guardianName: 'Agus Putra' }) }] }))
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText('The form already matches what the AI read.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Use all suggestions' })).toBeNull()
  })

  it('shows what the documents do not agree on', async () => {
    serveApplicant(extractions({ mismatches: [{ field: 'dateOfBirth', values: [{ source: 'Form', value: '2013-05-17' }, { source: 'BirthCertificate', value: '2014-05-05' }] }] }))
    renderPage(<MyApplicationPage />)

    expect(await screen.findByText(/The documents do not agree/)).toBeTruthy()
    const item = screen.getByText('Date of birth', { selector: 'p' }).closest('li')!
    expect(within(item).getByText(/Form:/)).toBeTruthy()
    expect(within(item).getByText('2014-05-05')).toBeTruthy()
  })

  it('shows no suggestions before anything was read', async () => {
    serveApplicant(extractions({ documents: [] }))
    renderPage(<MyApplicationPage />)

    await screen.findByRole('button', { name: /Read with AI.*Birth certificate/ })
    expect(screen.queryByText('Suggestions from your documents')).toBeNull()
  })
})

describe('suggestionsFor', () => {
  it('takes each field from the first document that has it, in the order birth certificate, family card, report card', () => {
    const result = suggestionsFor(form, {
      mismatches: [],
      documents: [
        { type: 'ReportCard', extractedAt: '', fields: fields({ fullName: 'From report' }) },
        { type: 'FamilyCard', extractedAt: '', fields: fields({ fullName: 'From card', address: 'Card street' }) },
      ],
    })

    expect(result).toEqual([
      { field: 'fullName', value: 'From card', source: 'FamilyCard' },
      { field: 'address', value: 'Card street', source: 'FamilyCard' },
    ])
  })
})

describe('the staff and the AI reading of the documents', () => {
  const detail = (): ApplicantDetail => ({
    id: 'a1',
    email: 'rina@example.test',
    status: 'InVerification',
    form,
    notes: null,
    submittedAt: '2026-10-20T03:00:00Z',
    decidedAt: null,
    reregistrationConfirmedAt: null,
    studentId: null,
    periodName: 'Admissions 2027/2028',
    documents: [
      { type: 'BirthCertificate', fileName: 'akta.png', status: 'Pending', reviewNote: null },
      { type: 'FamilyCard', fileName: 'kk.pdf', status: 'Pending', reviewNote: null },
      { type: 'ReportCard', fileName: 'rapor.jpg', status: 'Pending', reviewNote: null },
    ],
  })

  const serveStaff = (read: ApplicantExtractions) =>
    http.customInstance.mockImplementation(async ({ url, method }: Config) => {
      const key = `${method} ${url}`
      if (key === 'GET /api/v1/admissions/applicants/a1') return detail()
      if (key === 'GET /api/v1/admissions/applicants/a1/extractions') return read
      if (key.endsWith('/extract')) return read.documents[0]
      throw new Error(`unexpected request ${key}`)
    })

  const render = () => renderRoutes([{ path: '/manage/admissions/:id', element: <ApplicantDetailPage /> }], '/manage/admissions/a1')

  beforeEach(() => signInAs('Admin', 'M Ridhwan Habib A'))

  it('lets staff read a photo with the AI, but not a PDF', async () => {
    serveStaff(extractions())
    render()

    expect(await screen.findByRole('button', { name: /Read with AI.*Birth certificate/ })).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Read with AI.*Family card/ })).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /Read with AI.*Latest report card/ }))
    await waitFor(() => expect(calls('POST', '/api/v1/admissions/applicants/a1/documents/ReportCard/extract')).toHaveLength(1))
  })

  it('shows what was read per document, including NIK, parents and grades, and flags what disagrees', async () => {
    serveStaff(
      extractions({
        documents: [
          { type: 'FamilyCard', extractedAt: '2026-11-03T03:00:00Z', fields: fields({ nik: '3201014505140001', fatherName: 'Budi Santoso', motherName: 'Sari Lestari' }) },
          { type: 'ReportCard', extractedAt: '2026-11-03T03:00:00Z', fields: fields({ grades: [{ subject: 'Mathematics', score: 82 }, { subject: 'English', score: 78 }] }) },
        ],
        mismatches: [{ field: 'dateOfBirth', values: [{ source: 'BirthCertificate', value: '2014-05-05' }, { source: 'FamilyCard', value: '2014-06-17' }] }],
      }),
    )
    render()

    expect(await screen.findByText('What the AI read from the documents')).toBeTruthy()
    expect(await screen.findByText('3201014505140001')).toBeTruthy()
    expect(screen.getByText('Budi Santoso')).toBeTruthy()
    expect(screen.getByText('Mathematics 82, English 78')).toBeTruthy()
    expect(screen.getByText('These do not agree between the documents and the form:')).toBeTruthy()
    expect(screen.getByText('2014-06-17')).toBeTruthy()
  })

  it('says nothing has been read yet', async () => {
    serveStaff(extractions({ documents: [] }))
    render()

    expect(await screen.findByText('Nothing has been read yet. Use "Read with AI" on a document.')).toBeTruthy()
  })
})
