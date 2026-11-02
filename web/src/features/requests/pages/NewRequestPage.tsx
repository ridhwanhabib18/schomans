import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useOwnStudentId } from '@/shared/hooks/useOwnStudentId'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { ChildPicker } from '@/shared/ui/child-picker'
import { FormField } from '@/shared/ui/form-field'
import { Input, Select, Textarea } from '@/shared/ui/form-controls'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateRequest } from '../api/requests'
import type { RequestType } from '../api/types'
import { requestErrorText } from '../errors'
import '../i18n'

/** The kinds each role may ask for; the API enforces the same rules. */
const KINDS: Record<string, readonly RequestType[]> = {
  Parent: ['StudentLeave', 'CertificateLetter'],
  Student: ['CertificateLetter'],
  Teacher: ['TeacherLeave'],
}

type FieldKey = 'days' | 'reason' | 'order'

function RequestForm({ studentId, kinds }: { studentId: string | null; kinds: readonly RequestType[] }) {
  const { t } = useTranslation('requests')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateRequest()
  const [type, setType] = useState<RequestType>(kinds[0]!)
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')
  const [errors, setErrors] = useState<Partial<Record<FieldKey, string>>>({})
  const [serverError, setServerError] = useState<string>()
  const isLetter = type === 'CertificateLetter'

  function submit() {
    const found: Partial<Record<FieldKey, string>> = {}
    if (!isLetter && (startDate === '' || endDate === '')) {
      found.days = t('new.needDays')
    } else if (!isLetter && endDate < startDate) {
      found.order = t('new.order')
    }
    if (reason.trim() === '') {
      found.reason = t('new.needReason')
    }
    setErrors(found)
    setServerError(undefined)
    if (Object.keys(found).length > 0) {
      return
    }

    create.mutate(
      { type, studentId, startDate: isLetter ? null : startDate, endDate: isLetter ? null : endDate, reason: reason.trim() },
      {
        onSuccess: (created) => {
          showToast({ kind: 'success', title: t('new.sent') })
          void navigate(`/requests/${created.id}`)
        },
        onError: (error) => setServerError(requestErrorText(error, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <form
      noValidate
      className="flex max-w-xl flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        submit()
      }}
    >
      {serverError && <Alert tone="error" message={serverError} />}
      {kinds.length > 1 ? (
        <FormField label={t('new.kind')} hint={t(`new.kindHint.${type}`)}>
          <Select value={type} onChange={(event) => setType(event.target.value as RequestType)}>
            {kinds.map((kind) => (
              <option key={kind} value={kind}>
                {t(`type.${kind}`)}
              </option>
            ))}
          </Select>
        </FormField>
      ) : (
        <p className="text-sm text-muted-foreground">{t(`new.kindHint.${kinds[0]!}`)}</p>
      )}
      {!isLetter && (
        <div className="grid gap-4 sm:grid-cols-2 [&>*]:min-w-0">
          <FormField label={t('new.firstDay')} error={errors.days} required>
            <Input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
          </FormField>
          <FormField label={t('new.lastDay')} error={errors.order} required>
            <Input type="date" value={endDate} min={startDate || undefined} onChange={(event) => setEndDate(event.target.value)} />
          </FormField>
        </div>
      )}
      <FormField
        label={isLetter ? t('new.reasonLetter') : t('new.reasonLeave')}
        hint={isLetter ? t('new.reasonLetterHint') : undefined}
        error={errors.reason}
        required
      >
        <Textarea rows={4} maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} />
      </FormField>
      <div>
        <Button type="submit" disabled={create.isPending}>
          {create.isPending ? t('new.sending') : t('new.submit')}
        </Button>
      </div>
    </form>
  )
}

function StudentRequestForm() {
  const { t } = useTranslation('requests')
  const { data: studentId, isPending, isError, refetch } = useOwnStudentId()

  if (isPending) {
    return <Skeleton className="h-48 max-w-xl" />
  }

  return isError ? <ErrorNotice message={t('new.studentError')} onRetry={() => void refetch()} /> : <RequestForm studentId={studentId} kinds={KINDS.Student!} />
}

/** A parent, a student or a teacher asks for a leave or a letter. */
export function NewRequestPage() {
  const { t } = useTranslation('requests')
  const role = useAuthStore(selectAuthRole)

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('new.title')} description={t('new.description')} />
      {role === 'Parent' && <ChildPicker>{(childId) => <RequestForm key={childId} studentId={childId} kinds={KINDS.Parent!} />}</ChildPicker>}
      {role === 'Student' && <StudentRequestForm />}
      {role === 'Teacher' && <RequestForm studentId={null} kinds={KINDS.Teacher!} />}
    </div>
  )
}
