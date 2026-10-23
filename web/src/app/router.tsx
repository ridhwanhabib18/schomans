import { createBrowserRouter, type RouteObject } from 'react-router'
import { CallbackPage } from '@/features/auth/pages/CallbackPage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireRole } from '@/features/auth/components/RequireRole'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { LandingPage } from '@/features/landing/pages/LandingPage'
import { APP_ROLES } from '@/shared/lib/roles'
import { AppLayout } from './layouts/AppLayout'
import { NotFoundPage } from './NotFoundPage'

/** Routes are exported separately so tests can mount them in a memory router. */
export const routes: RouteObject[] = [
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/auth/callback', element: <CallbackPage /> },
  {
    // Everything below needs a signed-in user; each page then narrows it to the roles that may open it.
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          {
            path: '/dashboard',
            element: (
              <RequireRole roles={APP_ROLES}>
                <DashboardPage />
              </RequireRole>
            ),
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]

export const createAppRouter = () => createBrowserRouter(routes)
