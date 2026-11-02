import { Check } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/shared/lib/utils'
import type { ApplicantStatus } from '../api/admissions'
import '../i18n'

/** The road of an application. The fourth stop is the decision: accepted, or rejected when that is what happened. */
function stagesFor(status: ApplicantStatus): ApplicantStatus[] {
  return ['Draft', 'Submitted', 'InVerification', status === 'Rejected' ? 'Rejected' : 'Accepted', 'Enrolled']
}

/** Where an application stands, with the stage the applicant is allowed to see marked as the current one. */
export function StatusTracker({ status }: { status: ApplicantStatus }) {
  const { t } = useTranslation('admissions')
  const stages = stagesFor(status)
  const current = stages.indexOf(status)

  return (
    <section aria-label={t('mine.tracker')} className="min-w-0 rounded-xl border border-border bg-card p-4">
      <ol className="flex flex-wrap gap-x-6 gap-y-3">
        {stages.map((stage, index) => {
          const done = index < current
          const isCurrent = index === current
          return (
            <li key={stage} aria-current={isCurrent ? 'step' : undefined} className="flex min-w-0 items-center gap-2 text-sm">
              <span
                aria-hidden="true"
                className={cn(
                  'grid size-6 shrink-0 place-items-center rounded-full border text-xs font-semibold',
                  done && 'border-primary bg-primary text-primary-foreground',
                  isCurrent && stage === 'Rejected' && 'border-destructive bg-destructive text-white',
                  isCurrent && stage !== 'Rejected' && 'border-primary text-primary',
                  !done && !isCurrent && 'border-border text-muted-foreground',
                )}
              >
                {done ? <Check className="size-3.5" /> : index + 1}
              </span>
              <span className={cn(isCurrent ? 'font-semibold' : 'text-muted-foreground')}>{t(`status.${stage}`)}</span>
            </li>
          )
        })}
      </ol>
      <p className="mt-3 text-sm text-muted-foreground">{t(`statusHelp.${status}`)}</p>
    </section>
  )
}
