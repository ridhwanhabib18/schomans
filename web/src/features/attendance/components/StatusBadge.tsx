import { useTranslation } from 'react-i18next'
import type { AttendanceStatus } from '@/api/generated/model'
import { Badge } from '@/shared/ui/badge'
import '../i18n'

const variants = { Present: 'success', Excused: 'default', Sick: 'warning', Absent: 'destructive' } as const

/** An attendance status as a coloured badge with its name (never colour alone). Null means not scanned yet. */
export function StatusBadge({ status }: { status: AttendanceStatus | null | undefined }) {
  const { t } = useTranslation('attendance')

  return status ? <Badge variant={variants[status]}>{t(`status.${status}`)}</Badge> : <Badge variant="muted">{t('status.waiting')}</Badge>
}
