import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { formatDateTime } from '@/shared/lib/format'
import { buttonVariants } from '@/shared/ui/button'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { useOpenAdmission } from '../api/admissions'
import '../i18n'

/**
 * The admissions banner of the landing page: applications are open until a date, or will open on one. Nothing shows when
 * the school has no period to announce or the answer fails: the landing page still works. Only a guest or an applicant
 * gets the button, because a signed-in teacher or parent has nothing to apply for.
 */
export function AdmissionsBanner() {
  const { t } = useTranslation('admissions')
  const role = useAuthStore(selectAuthRole)
  const { data: period } = useOpenAdmission()

  if (!period) {
    return null
  }

  return (
    <section aria-labelledby="admissions-banner" className="flex min-w-0 flex-col gap-3 rounded-xl border border-primary/30 bg-primary/5 p-5 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <h2 id="admissions-banner" className="text-lg font-semibold">
          {period.name}
        </h2>
        <p className="text-sm text-muted-foreground">
          {period.isOpen ? t('banner.open', { date: formatDateTime(period.closesAt) }) : t('banner.soon', { date: formatDateTime(period.opensAt) })}
        </p>
      </div>
      {period.isOpen && (role === null || role === 'Applicant') && (
        <Link to="/admissions" className={buttonVariants({ size: 'lg' })}>
          {t('banner.apply')}
        </Link>
      )}
    </section>
  )
}
