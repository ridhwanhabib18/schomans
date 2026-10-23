import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { AppRole } from '@/shared/lib/roles'
import { buttonVariants } from '@/shared/ui/button'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import '../i18n'

interface RequireRoleProps {
  roles: readonly AppRole[]
  children: ReactNode
}

/**
 * Route guard per role. This only shapes the UI; the API enforces the same rules with a policy on every endpoint,
 * so hiding a page here is never the security boundary.
 */
export function RequireRole({ roles, children }: RequireRoleProps) {
  const { t } = useTranslation()
  const role = useAuthStore(selectAuthRole)

  if (role && roles.includes(role)) {
    return children
  }

  return (
    <div role="alert" className="mx-auto flex max-w-md flex-col items-center gap-3 py-16 text-center">
      <h1 className="text-xl font-semibold">{t('errors.forbidden.title')}</h1>
      <p className="text-muted-foreground">{t('errors.forbidden.body')}</p>
      <Link to="/dashboard" className={buttonVariants({ variant: 'outline' })}>
        {t('nav.dashboard')}
      </Link>
    </div>
  )
}
