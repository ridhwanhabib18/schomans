import { useTranslation } from 'react-i18next'
import { Link, NavLink, Outlet } from 'react-router'
import { cn } from '@/shared/lib/utils'
import { buttonVariants } from '@/shared/ui/button'
import { selectAuthStatus, useAuthStore } from '@/stores/useAuthStore'
import { ThemeToggle } from './ThemeToggle'

/** Shell of the pages everybody can open: the landing page, the news, a public announcement. */
export function PublicLayout() {
  const { t } = useTranslation()
  const status = useAuthStore(selectAuthStatus)

  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2"
      >
        {t('nav.skipToContent')}
      </a>

      <header className="border-b border-border bg-card">
        <div className="mx-auto flex w-full max-w-5xl items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-6">
            <Link to="/" className="text-lg font-bold tracking-tight">
              {t('app.name')}
            </Link>
            <nav aria-label={t('nav.publicMain')} className="flex gap-4 text-sm font-medium">
              {[
                { to: '/', label: t('nav.home'), end: true },
                { to: '/news', label: t('nav.news'), end: false },
              ].map(({ to, label, end }) => (
                <NavLink key={to} to={to} end={end} className={({ isActive }) => cn('hover:text-primary', isActive && 'text-primary')}>
                  {label}
                </NavLink>
              ))}
            </nav>
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            {status === 'authenticated' ? (
              <Link to="/dashboard" className={buttonVariants({ size: 'sm' })}>
                {t('nav.dashboard')}
              </Link>
            ) : (
              <Link to="/login" className={buttonVariants({ size: 'sm' })}>
                {t('actions.signIn')}
              </Link>
            )}
          </div>
        </div>
      </header>

      <main id="main" className="mx-auto w-full max-w-5xl flex-1 p-4 py-8 sm:p-8">
        <Outlet />
      </main>
    </div>
  )
}
