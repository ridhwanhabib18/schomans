import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import type { PublicAdmissionPeriod } from '../api/admissions'
import '../i18n'

/** The four moments of a period, in school time. */
export function PeriodDates({ period }: { period: PublicAdmissionPeriod }) {
  const { t } = useTranslation('admissions')
  const rows = [
    ['opens', period.opensAt],
    ['closes', period.closesAt],
    ['results', period.resultsAnnouncedAt],
    ['deadline', period.reregistrationDeadline],
  ] as const

  return (
    <section aria-label={t('period.title')} className="min-w-0 rounded-xl border border-border bg-card p-4">
      <h2 className="text-sm font-semibold">{period.name}</h2>
      <dl className="mt-3 grid gap-3 text-sm sm:grid-cols-2">
        {rows.map(([key, value]) => (
          <div key={key} className="min-w-0">
            <dt className="text-muted-foreground">{t(`period.${key}`)}</dt>
            <dd className="font-medium tabular-nums">{formatDateTime(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
