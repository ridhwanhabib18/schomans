import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { currentMonth, formatMonth, shiftMonth } from '@/shared/lib/time'
import { Button } from './button'

/** Previous and next month around a label. The future is not offered: there is no attendance yet. */
export function MonthPicker({ month, onChange }: { month: string; onChange: (month: string) => void }) {
  const { t } = useTranslation()
  const isCurrent = month >= currentMonth()

  return (
    <div className="flex items-center gap-2" role="group" aria-label={t('month.label')}>
      <Button variant="outline" size="icon" aria-label={t('month.previous')} onClick={() => onChange(shiftMonth(month, -1))}>
        <ChevronLeft aria-hidden="true" />
      </Button>
      <span aria-live="polite" className="min-w-36 text-center font-medium">
        {formatMonth(month)}
      </span>
      <Button variant="outline" size="icon" aria-label={t('month.next')} disabled={isCurrent} onClick={() => onChange(shiftMonth(month, 1))}>
        <ChevronRight aria-hidden="true" />
      </Button>
    </div>
  )
}
