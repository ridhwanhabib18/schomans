import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import '../i18n'

/** The top of the landing page: what SchoMans is, and the way in. */
export function LandingHero() {
  const { t } = useTranslation('landing')
  const { t: tCommon } = useTranslation()

  return (
    <header className="flex flex-col gap-4">
      <p className="text-sm font-semibold uppercase tracking-wide text-primary">{tCommon('app.name')}</p>
      <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">{t('hero.title')}</h1>
      <p className="max-w-2xl text-lg text-muted-foreground">{t('hero.body')}</p>
      <div>
        <Link to="/login" className={buttonVariants({ size: 'lg' })}>
          {tCommon('actions.signIn')}
        </Link>
      </div>
    </header>
  )
}
