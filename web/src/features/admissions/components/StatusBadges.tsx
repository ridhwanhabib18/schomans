import { useTranslation } from 'react-i18next'
import { Badge } from '@/shared/ui/badge'
import type { ApplicantStatus, DocumentStatus } from '../api/admissions'
import '../i18n'

const APPLICANT_VARIANT: Record<ApplicantStatus, 'muted' | 'warning' | 'success' | 'destructive' | 'default'> = {
  Draft: 'muted',
  Submitted: 'warning',
  InVerification: 'warning',
  Accepted: 'success',
  Rejected: 'destructive',
  Enrolled: 'default',
}

const DOCUMENT_VARIANT: Record<DocumentStatus, 'muted' | 'success' | 'destructive'> = {
  Pending: 'muted',
  Verified: 'success',
  Rejected: 'destructive',
}

export function ApplicantStatusBadge({ status }: { status: ApplicantStatus }) {
  const { t } = useTranslation('admissions')
  return <Badge variant={APPLICANT_VARIANT[status]}>{t(`status.${status}`)}</Badge>
}

export function DocumentStatusBadge({ status }: { status: DocumentStatus }) {
  const { t } = useTranslation('admissions')
  return <Badge variant={DOCUMENT_VARIANT[status]}>{t(`docStatus.${status}`)}</Badge>
}
