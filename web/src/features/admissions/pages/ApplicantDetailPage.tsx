import { ArrowLeft, FileText } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useActiveClassroomOptions } from '@/shared/hooks/useActiveClassroomOptions'
import { formatDate, formatDateTime } from '@/shared/lib/format'
import { cn } from '@/shared/lib/utils'
import { Alert } from '@/shared/ui/alert'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import {
  DOCUMENT_KINDS,
  useApplicant,
  useDecideApplicant,
  useEnrollApplicant,
  useOpenApplicantDocument,
  useReviewDocument,
  type ApplicantDetail,
  type DocumentType,
} from '../api/admissions'
import { NoteDialog } from '../components/NoteDialog'
import { ApplicantStatusBadge, DocumentStatusBadge } from '../components/StatusBadges'
import { admissionErrorText } from '../errors'
import '../i18n'

const NISN = /^\d{10}$/

type Asking = { kind: 'reject-document'; type: DocumentType } | { kind: 'accept' } | { kind: 'reject-applicant' } | null

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex min-w-0 flex-col gap-3 rounded-xl border border-border bg-card p-4">
      <h2 className="text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}

function EnrollForm({ applicant, onError }: { applicant: ApplicantDetail; onError: (message: string | undefined) => void }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const classrooms = useActiveClassroomOptions()
  const enroll = useEnrollApplicant()
  const [classroomId, setClassroomId] = useState('')
  const [nisn, setNisn] = useState('')
  const [touched, setTouched] = useState(false)
  const nisnProblem = touched && !NISN.test(nisn) ? t('detail.enroll.nisnInvalid') : undefined

  return (
    <Section title={t('detail.enroll.title')}>
      <p className="text-sm text-muted-foreground">{t('detail.enroll.body')}</p>
      <form
        noValidate
        className="grid min-w-0 gap-4 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault()
          setTouched(true)
          onError(undefined)
          if (!NISN.test(nisn) || classroomId === '') {
            return
          }

          enroll.mutate(
            { id: applicant.id, classroomId, nisn },
            {
              onSuccess: () => showToast({ kind: 'success', title: t('detail.enroll.done') }),
              onError: (e) => onError(admissionErrorText(e, t, tCommon('errors.generic'))),
            },
          )
        }}
      >
        <FormField label={t('detail.enroll.classroom')} required>
          <Select value={classroomId} onChange={(event) => setClassroomId(event.target.value)}>
            <option value="">{t('detail.enroll.choose')}</option>
            {(classrooms.data ?? []).map((classroom) => (
              <option key={classroom.id} value={classroom.id}>
                {classroom.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('detail.enroll.nisn')} required hint={t('detail.enroll.nisnHint')} error={nisnProblem}>
          <Input value={nisn} inputMode="numeric" maxLength={10} onChange={(event) => setNisn(event.target.value)} />
        </FormField>
        <div className="sm:col-span-2">
          <Button type="submit" disabled={enroll.isPending}>
            {t('detail.enroll.button')}
          </Button>
        </div>
      </form>
    </Section>
  )
}

/** One application for the admin staff: the form, the documents to verify, the decision and, at the end, the enrollment. */
export function ApplicantDetailPage() {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const { id = '' } = useParams()
  const showToast = useToastStore(selectShowToast)
  const { data: applicant, isPending, isError, refetch } = useApplicant(id)
  const openDocument = useOpenApplicantDocument()
  const review = useReviewDocument()
  const decide = useDecideApplicant()
  const [asking, setAsking] = useState<Asking>(null)
  const [error, setError] = useState<string>()

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="flex flex-col gap-3">
        <Skeleton className="h-12 w-64" />
        <Skeleton className="h-48 w-full" />
      </div>
    )
  }

  if (isError) {
    return <ErrorNotice message={t('detail.error')} onRetry={() => void refetch()} />
  }

  const inReview = applicant.status === 'Submitted' || applicant.status === 'InVerification'
  const fail = (e: unknown) => {
    setError(admissionErrorText(e, t, tCommon('errors.generic')))
    setAsking(null)
  }
  const form = applicant.form
  const formRows: [string, string][] = [
    [t('form.fullName'), form.fullName],
    [t('form.dateOfBirth'), form.dateOfBirth ? formatDate(form.dateOfBirth) : '—'],
    [t('form.placeOfBirth'), form.placeOfBirth],
    [t('form.address'), form.address],
    [t('form.guardianName'), form.guardianName],
    [t('form.guardianPhone'), form.guardianPhone],
    [t('form.previousSchool'), form.previousSchool],
  ]

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <Link to="/manage/admissions" className={cn(buttonVariants({ variant: 'link', size: 'sm' }), 'self-start')}>
        <ArrowLeft aria-hidden="true" />
        {t('detail.back')}
      </Link>
      <PageHeader
        title={form.fullName || applicant.email}
        description={`${applicant.email} · ${applicant.periodName}`}
        actions={<ApplicantStatusBadge status={applicant.status} />}
      />

      {error && <Alert tone="error" message={error} />}

      <Section title={t('detail.form')}>
        <dl className="grid min-w-0 gap-3 text-sm sm:grid-cols-2">
          {formRows.map(([label, value]) => (
            <div key={label} className="min-w-0">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="break-words font-medium">{value || '—'}</dd>
            </div>
          ))}
        </dl>
      </Section>

      <Section title={t('detail.documents')}>
        <ul className="flex flex-col gap-3">
          {DOCUMENT_KINDS.map((type) => {
            const document = applicant.documents.find((d) => d.type === type)
            const label = t(`docTypes.${type}`)
            return (
              <li key={type} className="flex min-w-0 flex-col gap-2 rounded-lg border border-border p-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <p className="font-medium">{label}</p>
                  {document ? (
                    <p className="flex min-w-0 flex-wrap items-center gap-2 text-sm text-muted-foreground">
                      <FileText aria-hidden="true" className="size-4 shrink-0" />
                      <span className="truncate">{document.fileName}</span>
                      <DocumentStatusBadge status={document.status} />
                    </p>
                  ) : (
                    <p className="text-sm text-muted-foreground">{t('detail.notUploaded')}</p>
                  )}
                  {document?.reviewNote && <p className="mt-1 text-sm text-destructive">{t('documents.note', { note: document.reviewNote })}</p>}
                </div>
                {document && (
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={openDocument.isPending}
                      onClick={() => openDocument.mutate({ id: applicant.id, type }, { onError: fail })}
                    >
                      {t('detail.view')}
                      <span className="sr-only">: {label}</span>
                    </Button>
                    {inReview && (
                      <>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={review.isPending || document.status === 'Verified'}
                          onClick={() => {
                            setError(undefined)
                            review.mutate(
                              { id: applicant.id, type, verified: true },
                              { onSuccess: () => showToast({ kind: 'success', title: t('detail.verified') }), onError: fail },
                            )
                          }}
                        >
                          {t('detail.verify')}
                          <span className="sr-only">: {label}</span>
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => setAsking({ kind: 'reject-document', type })}>
                          {t('detail.reject')}
                          <span className="sr-only">: {label}</span>
                        </Button>
                      </>
                    )}
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      </Section>

      <Section title={t('detail.decision')}>
        {applicant.decidedAt && <p className="text-sm text-muted-foreground">{t('detail.decidedAt', { date: formatDateTime(applicant.decidedAt) })}</p>}
        {applicant.notes && (
          <p className="text-sm">
            <span className="font-medium">{t('detail.staffNote')}: </span>
            {applicant.notes}
          </p>
        )}
        {inReview ? (
          <>
            <p className="text-sm text-muted-foreground">{t('detail.decisionHint')}</p>
            <div className="flex flex-wrap gap-2">
              <Button onClick={() => setAsking({ kind: 'accept' })}>{t('detail.accept')}</Button>
              <Button variant="outline" onClick={() => setAsking({ kind: 'reject-applicant' })}>
                {t('detail.rejectApplicant')}
              </Button>
            </div>
          </>
        ) : null}
      </Section>

      {applicant.status === 'Accepted' &&
        (applicant.reregistrationConfirmedAt ? (
          <EnrollForm applicant={applicant} onError={setError} />
        ) : (
          <Alert tone="info" message={t('detail.enroll.waiting')} />
        ))}
      {applicant.status === 'Enrolled' && <Alert tone="info" message={t('detail.enroll.enrolled')} />}

      {asking?.kind === 'reject-document' && (
        <NoteDialog
          title={t('detail.rejectTitle')}
          description={t('detail.rejectBody')}
          label={t('detail.rejectNote')}
          confirmLabel={t('detail.rejectConfirm')}
          pending={review.isPending}
          onClose={() => setAsking(null)}
          onConfirm={(note) => {
            setError(undefined)
            review.mutate(
              { id: applicant.id, type: asking.type, verified: false, note },
              {
                onSuccess: () => {
                  showToast({ kind: 'success', title: t('detail.rejected') })
                  setAsking(null)
                },
                onError: fail,
              },
            )
          }}
        />
      )}

      {asking?.kind === 'reject-applicant' && (
        <NoteDialog
          title={t('detail.rejectApplicantTitle')}
          description={t('detail.rejectApplicantBody')}
          label={t('detail.note')}
          confirmLabel={t('detail.rejectApplicant')}
          pending={decide.isPending}
          onClose={() => setAsking(null)}
          onConfirm={(note) => {
            setError(undefined)
            decide.mutate(
              { id: applicant.id, accept: false, note },
              {
                onSuccess: () => {
                  showToast({ kind: 'success', title: t('detail.decided') })
                  setAsking(null)
                },
                onError: fail,
              },
            )
          }}
        />
      )}

      <ConfirmDialog
        open={asking?.kind === 'accept'}
        onOpenChange={(open) => !open && setAsking(null)}
        title={t('detail.acceptTitle')}
        description={t('detail.acceptBody')}
        confirmLabel={t('detail.accept')}
        pending={decide.isPending}
        onConfirm={() => {
          setError(undefined)
          decide.mutate(
            { id: applicant.id, accept: true },
            {
              onSuccess: () => {
                showToast({ kind: 'success', title: t('detail.decided') })
                setAsking(null)
              },
              onError: fail,
            },
          )
        }}
      />
    </div>
  )
}
