import { useTranslation } from 'react-i18next'
import '../i18n'

const FACTS = ['students', 'classes', 'teachers', 'subjects'] as const

/** The school profile of the landing page: a short text about the (fictional) school and a few numbers. */
export function LandingProfile() {
  const { t } = useTranslation('landing')

  return (
    <section aria-labelledby="school-profile" className="flex min-w-0 flex-col gap-4">
      <h2 id="school-profile" className="text-xl font-semibold">
        {t('profile.title')}
      </h2>
      <p className="max-w-3xl text-muted-foreground">{t('profile.body')}</p>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {FACTS.map((fact) => (
          <div key={fact} className="min-w-0 rounded-lg border border-border bg-card p-4">
            <dt className="text-sm text-muted-foreground">{t(`profile.facts.${fact}.label`)}</dt>
            <dd className="text-2xl font-semibold tabular-nums">{t(`profile.facts.${fact}.value`)}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
