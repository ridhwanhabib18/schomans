import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthStatus, useAuthStore } from '@/stores/useAuthStore'

/** Route guard: only signed-in users pass. Anonymous visitors are sent to the login page and come back afterwards. */
export function RequireAuth() {
  const { t } = useTranslation()
  const status = useAuthStore(selectAuthStatus)
  const location = useLocation()

  if (status === 'loading') {
    return (
      <div className="grid min-h-dvh place-items-center p-8" role="status" aria-busy="true">
        <div className="flex w-full max-w-sm flex-col gap-3">
          <Skeleton className="h-8 w-2/3" />
          <Skeleton className="h-24 w-full" />
          <span className="sr-only">{t('session.restoring')}</span>
        </div>
      </div>
    )
  }

  if (status === 'anonymous') {
    // The query string comes along: a check-in address carries its code in it (/attend?t=...).
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return <Outlet />
}
