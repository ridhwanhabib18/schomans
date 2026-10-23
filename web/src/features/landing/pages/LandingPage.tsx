import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import '../i18n'

/** Minimal public page for guests. The real landing page (news, admissions banner) arrives with Phase 5. */
export function LandingPage() {
  const { t } = useTranslation('landing')
  const { t: tCommon } = useTranslation()

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-3xl flex-col justify-center gap-8 p-4 sm:p-8">
      <header className="flex flex-col gap-4">
        <p className="text-sm font-semibold uppercase tracking-wide text-primary">{tCommon('app.name')}</p>
        <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">{t('hero.title')}</h1>
        <p className="text-lg text-muted-foreground">{t('hero.body')}</p>
        <div>
          <Link to="/login" className={buttonVariants({ size: 'lg' })}>
            {tCommon('actions.signIn')}
          </Link>
        </div>
      </header>
      <Card>
        <CardHeader>
          <CardTitle>{t('news.title')}</CardTitle>
          <CardDescription>{t('news.placeholder')}</CardDescription>
        </CardHeader>
        <CardContent />
      </Card>
    </main>
  )
}
