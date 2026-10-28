import { LogOut } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { useLogout } from '@/features/auth/api/useAuth'
import { NotificationBell } from '@/features/notifications/components/NotificationBell'
import { cn } from '@/shared/lib/utils'
import { Button } from '@/shared/ui/button'
import { selectAuthRole, selectAuthUser, useAuthStore } from '@/stores/useAuthStore'
import { navItemsFor, navSectionsFor } from './navigation'
import { JobsDashboardLink } from './JobsDashboardLink'
import { ThemeToggle } from './ThemeToggle'

/** Shell of every signed-in page: sidebar on desktop, bottom navigation on phones, header with the bell and the user. */
export function AppLayout() {
  const { t } = useTranslation()
  const user = useAuthStore(selectAuthUser)
  const role = useAuthStore(selectAuthRole)
  const logout = useLogout()
  const sections = navSectionsFor(role)
  const items = navItemsFor(role)

  return (
    <div className="min-h-dvh md:grid md:grid-cols-[15rem_1fr]">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2"
      >
        {t('nav.skipToContent')}
      </a>

      <aside className="hidden border-r border-border bg-card md:block">
        <div className="sticky top-0 flex max-h-dvh flex-col gap-6 overflow-y-auto p-4">
          <p className="px-2 text-lg font-bold tracking-tight">{t('app.name')}</p>
          <nav aria-label={t('nav.main')} className="flex flex-col gap-5">
            {sections.map(({ section, items: sectionItems }) => (
              <div key={section} className="flex flex-col gap-1">
                {section !== 'main' && (
                  <p className="px-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(`nav.sections.${section}`)}</p>
                )}
                {sectionItems.map(({ to, labelKey, icon: Icon, end }) => (
                  <NavLink
                    key={to}
                    to={to}
                    end={end}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium hover:bg-muted',
                        isActive && 'bg-muted text-primary',
                      )
                    }
                  >
                    <Icon aria-hidden="true" className="size-4" />
                    {t(labelKey)}
                  </NavLink>
                ))}
              </div>
            ))}
          </nav>
          {role === 'Admin' && <JobsDashboardLink />}
        </div>
      </aside>

      <div className="flex min-w-0 flex-col">
        <header className="flex items-center justify-between gap-3 border-b border-border bg-card px-4 py-3">
          <p className="font-bold tracking-tight md:invisible">{t('app.name')}</p>
          <div className="flex items-center gap-2">
            <NotificationBell />
            <div className="hidden text-right text-sm leading-tight sm:block">
              <p className="font-medium">{user?.name}</p>
              {role && <p className="text-xs text-muted-foreground">{t(`roles.${role}`)}</p>}
            </div>
            <ThemeToggle />
            <Button variant="ghost" size="sm" disabled={logout.isPending} onClick={() => logout.mutate()}>
              <LogOut aria-hidden="true" />
              {t('actions.signOut')}
            </Button>
          </div>
        </header>

        <main id="main" className="flex-1 p-4 pb-24 md:p-8">
          <Outlet />
        </main>
      </div>

      {/* On a phone the entries scroll sideways: a teacher or admin has more than five. */}
      <nav
        aria-label={t('nav.main')}
        className="fixed inset-x-0 bottom-0 z-40 flex overflow-x-auto border-t border-border bg-card md:hidden"
      >
        {items.map(({ to, labelKey, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              cn(
                'flex min-w-20 flex-1 shrink-0 flex-col items-center gap-1 px-2 py-2 text-xs font-medium',
                isActive ? 'text-primary' : 'text-muted-foreground',
              )
            }
          >
            <Icon aria-hidden="true" className="size-5" />
            {t(labelKey)}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
