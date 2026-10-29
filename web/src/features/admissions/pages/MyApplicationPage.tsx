import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import {
  useConfirmReregistration,
  useMyAdmission,
  useSaveApplicantForm,
  useStartApplication,
  useSubmitApplication,
  type ApplicantForm,
  type MyApplication,
  type PublicAdmissionPeriod,
} from '../api/admissions'
import { ApplicationForm } from '../components/ApplicationForm'
import { DocumentsSection } from '../components/DocumentsSection'
import { PeriodDates } from '../components/PeriodDates'
import { StatusTracker } from '../components/StatusTracker'
import { admissionErrorText } from '../errors'
import '../i18n'

const sameForm = (a: ApplicantForm, b: ApplicantForm) => JSON.stringify(a) === JSON.stringify(b)

const formIsComplete = (form: ApplicantForm) =>
  [form.fullName, form.dateOfBirth, form.placeOfBirth, form.address, form.guardianName, form.guardianPhone, form.previousSchool].every((value) => (value ?? '').trim() !== '')

function Step({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex min-w-0 flex-col gap-4 rounded-xl border border-border bg-card p-4">
      <h2 className="text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}

/** No application yet and a period is open: explain and start. */
function StartCard({ period }: { period: PublicAdmissionPeriod }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const start = useStartApplication()
  const [error, setError] = useState<string>()

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <PeriodDates period={period} />
      <Step title={t('mine.start.title')}>
        <p className="text-sm text-muted-foreground">{t('mine.start.body')}</p>
        {error && <Alert tone="error" message={error} />}
        <div>
          <Button
            disabled={start.isPending}
            onClick={() => {
              setError(undefined)
              start.mutate(undefined, { onError: (e) => setError(admissionErrorText(e, t, tCommon('errors.generic'))) })
            }}
          >
            {t('mine.start.button')}
          </Button>
        </div>
      </Step>
    </div>
  )
}

/** The accepted applicant confirms the place (re-registration) before the deadline. */
function ReregistrationCard({ application }: { application: MyApplication }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const confirm = useConfirmReregistration()
  const [error, setError] = useState<string>()

  if (application.reregistrationConfirmed) {
    return <Alert tone="info" message={t('reregistration.confirmed')} />
  }

  if (!application.canConfirmReregistration) {
    return null
  }

  return (
    <Step title={t('reregistration.title')}>
      <p className="text-sm text-muted-foreground">{t('reregistration.body')}</p>
      {error && <Alert tone="error" message={error} />}
      <div>
        <Button
          disabled={confirm.isPending}
          onClick={() => {
            setError(undefined)
            confirm.mutate(undefined, {
              onSuccess: () => showToast({ kind: 'success', title: t('reregistration.done') }),
              onError: (e) => setError(admissionErrorText(e, t, tCommon('errors.generic'))),
            })
          }}
        >
          {t('reregistration.button')}
        </Button>
      </div>
    </Step>
  )
}

function ApplicationView({ application, period }: { application: MyApplication; period: PublicAdmissionPeriod | null }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSaveApplicantForm()
  const submit = useSubmitApplication()
  const [form, setForm] = useState(application.form)
  const [asking, setAsking] = useState(false)
  const [error, setError] = useState<string>()
  const dirty = !sameForm(form, application.form)
  const draft = application.status === 'Draft'

  function saveForm() {
    setError(undefined)
    save.mutate(form, {
      onSuccess: () => showToast({ kind: 'success', title: t('form.saved') }),
      onError: (e) => setError(admissionErrorText(e, t, tCommon('errors.generic'))),
    })
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <StatusTracker status={application.status} />
      {application.notes && <Alert tone="info" message={`${t('mine.note')}: ${application.notes}`} />}
      {period && <PeriodDates period={period} />}
      <ReregistrationCard application={application} />
      {error && <Alert tone="error" message={error} />}

      <Step title={t('mine.steps.form')}>
        <ApplicationForm value={form} onChange={setForm} editable={application.canEdit} dirty={dirty} saving={save.isPending} onSave={saveForm} />
      </Step>

      <Step title={t('mine.steps.documents')}>
        <DocumentsSection application={application} />
      </Step>

      {draft && (
        <Step title={t('mine.steps.submit')}>
          {!application.canEdit && <Alert tone="info" message={t('mine.closedDraft')} />}
          {dirty ? (
            <p className="text-sm text-muted-foreground">{t('submit.saveFirst')}</p>
          ) : !formIsComplete(application.form) ? (
            <p className="text-sm text-muted-foreground">{t('submit.needForm')}</p>
          ) : application.documents.length < 3 ? (
            <p className="text-sm text-muted-foreground">{t('submit.needDocuments')}</p>
          ) : null}
          <div>
            <Button disabled={!application.canSubmit || dirty || submit.isPending} onClick={() => setAsking(true)}>
              {t('submit.button')}
            </Button>
          </div>
        </Step>
      )}

      <ConfirmDialog
        open={asking}
        onOpenChange={setAsking}
        title={t('submit.confirmTitle')}
        description={t('submit.confirmBody')}
        confirmLabel={t('submit.confirm')}
        pending={submit.isPending}
        onConfirm={() =>
          submit.mutate(undefined, {
            onSuccess: () => {
              showToast({ kind: 'success', title: t('submit.done') })
              setAsking(false)
            },
            onError: (e) => {
              setError(admissionErrorText(e, t, tCommon('errors.generic')))
              setAsking(false)
            },
          })
        }
      />
    </div>
  )
}

/** The applicant's page: the whole application, step by step, with its status at every stage. */
export function MyApplicationPage() {
  const { t } = useTranslation('admissions')
  const { data, isPending, isError, refetch } = useMyAdmission()

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('mine.title')} description={t('mine.description')} />
      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('mine.error')} onRetry={() => void refetch()} />
      ) : data.application ? (
        <ApplicationView key={data.application.id} application={data.application} period={data.period} />
      ) : data.period?.isOpen ? (
        <StartCard period={data.period} />
      ) : (
        <EmptyState title={t('mine.noPeriod.title')} description={t('mine.noPeriod.body')} />
      )}
    </div>
  )
}
