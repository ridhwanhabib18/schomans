import { useTranslation } from 'react-i18next'
import type { StudentAssignmentStatus } from '@/api/generated/model'
import { Badge } from '@/shared/ui/badge'
import '../i18n'

/** Not submitted / submitted / late, and overdue when nothing came in and the due date has passed. */
export function AssignmentStatusBadge({ status, overdue }: { status: StudentAssignmentStatus; overdue?: boolean }) {
  const { t } = useTranslation('assignments')

  if (status === 'NotSubmitted') {
    return overdue ? <Badge variant="destructive">{t('status.overdue')}</Badge> : <Badge variant="muted">{t('status.NotSubmitted')}</Badge>
  }

  return <Badge variant={status === 'Late' ? 'warning' : 'success'}>{t(`status.${status}`)}</Badge>
}
