import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { NoRoleError } from '../api/authService'
import { useCompleteLogin } from '../api/useAuth'
import '../i18n'

/** Landing page of the Keycloak redirect: exchanges the code for tokens, then continues to where the user was going. */
export function CallbackPage() {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const completeLogin = useCompleteLogin()
  // React StrictMode runs effects twice in development; the authorization code can only be redeemed once.
  const started = useRef(false)

  useEffect(() => {
    if (started.current) {
      return
    }
    started.current = true
    completeLogin.mutate(undefined, { onSuccess: (path) => void navigate(path, { replace: true }) })
  }, [completeLogin, navigate])

  if (completeLogin.isError) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col items-center justify-center gap-4 p-8 text-center">
        <p role="alert">{completeLogin.error instanceof NoRoleError ? t('callback.noRole') : t('callback.failed')}</p>
        <Button asChild variant="outline">
          <Link to="/login">{t('callback.backToLogin')}</Link>
        </Button>
      </main>
    )
  }

  return (
    <main className="grid min-h-dvh place-items-center p-8" role="status" aria-busy="true">
      <div className="flex w-full max-w-sm flex-col items-center gap-3">
        <Skeleton className="h-6 w-1/2" />
        <p className="text-sm text-muted-foreground">{t('callback.completing')}</p>
      </div>
    </main>
  )
}
