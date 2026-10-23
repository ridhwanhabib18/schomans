import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { buttonVariants } from '@/shared/ui/button'

export function NotFoundPage() {
  const { t } = useTranslation()

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col items-center justify-center gap-3 p-8 text-center">
      <h1 className="text-2xl font-semibold">{t('errors.notFound.title')}</h1>
      <p className="text-muted-foreground">{t('errors.notFound.body')}</p>
      <Link to="/" className={buttonVariants({ variant: 'outline' })}>
        {t('actions.backHome')}
      </Link>
    </main>
  )
}
