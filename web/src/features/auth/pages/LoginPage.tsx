import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation } from 'react-router'
import { config } from '@/shared/lib/config'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { selectAuthStatus, useAuthStore } from '@/stores/useAuthStore'
import { DEFAULT_RETURN_PATH } from '../api/authService'
import { useStartLogin } from '../api/useAuth'
import { DemoLoginButtons } from '../components/DemoLoginButtons'
import '../i18n'

export function LoginPage() {
  const { t } = useTranslation('auth')
  const status = useAuthStore(selectAuthStatus)
  const location = useLocation()
  const startLogin = useStartLogin()

  const from = (location.state as { from?: string } | null)?.from
  const returnTo = from && from.startsWith('/') ? from : DEFAULT_RETURN_PATH

  if (status === 'authenticated') {
    return <Navigate to={returnTo} replace />
  }

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-2xl flex-col justify-center gap-6 p-4 sm:p-8">
      <Card>
        <CardHeader>
          <CardTitle className="text-2xl">{t('login.title')}</CardTitle>
          <CardDescription>{t('login.subtitle')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <Button size="lg" disabled={startLogin.isPending} onClick={() => startLogin.mutate(returnTo)}>
            {startLogin.isPending ? t('login.redirecting') : t('login.signIn')}
          </Button>
          {startLogin.isError && (
            <p role="alert" className="text-sm text-destructive">
              {t('login.failed')}
            </p>
          )}
          <Button asChild variant="link" className="self-start px-0">
            <Link to="/">{t('login.guest')}</Link>
          </Button>
        </CardContent>
      </Card>

      {config.demoMode && <DemoLoginButtons returnTo={returnTo} />}
    </main>
  )
}
