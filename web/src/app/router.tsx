import type { ReactNode } from 'react'
import { createBrowserRouter, type RouteObject } from 'react-router'
import { AnnouncementDetailPage } from '@/features/announcements/pages/AnnouncementDetailPage'
import { AnnouncementEditPage } from '@/features/announcements/pages/AnnouncementEditPage'
import { AnnouncementsInboxPage } from '@/features/announcements/pages/AnnouncementsInboxPage'
import { AnnouncementsManagePage } from '@/features/announcements/pages/AnnouncementsManagePage'
import { PublicAnnouncementPage } from '@/features/announcements/pages/PublicAnnouncementPage'
import { CallbackPage } from '@/features/auth/pages/CallbackPage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireRole } from '@/features/auth/components/RequireRole'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { AcademicYearsPage } from '@/features/masterdata/pages/AcademicYearsPage'
import { ClassroomsPage } from '@/features/masterdata/pages/ClassroomsPage'
import { RolloverPage } from '@/features/masterdata/pages/RolloverPage'
import { StudentsPage } from '@/features/masterdata/pages/StudentsPage'
import { SubjectsPage } from '@/features/masterdata/pages/SubjectsPage'
import { TeachersPage } from '@/features/masterdata/pages/TeachersPage'
import { NewsEditPage } from '@/features/news/pages/NewsEditPage'
import { NewsManagePage } from '@/features/news/pages/NewsManagePage'
import { PublicNewsArticlePage } from '@/features/news/pages/PublicNewsArticlePage'
import { PublicNewsListPage } from '@/features/news/pages/PublicNewsListPage'
import { APP_ROLES, type AppRole } from '@/shared/lib/roles'
import { AppLayout } from './layouts/AppLayout'
import { PublicLayout } from './layouts/PublicLayout'
import { RootLayout } from './layouts/RootLayout'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './NotFoundPage'

// The same groups as the API policies (Editors, StaffRead, Admin); the API stays the security boundary.
const ADMIN: readonly AppRole[] = ['Admin']
const EDITORS: readonly AppRole[] = ['Admin', 'Principal']
const STAFF: readonly AppRole[] = ['Admin', 'Principal', 'Teacher']

const guarded = (roles: readonly AppRole[], page: ReactNode) => <RequireRole roles={roles}>{page}</RequireRole>

/** Routes are exported separately so tests can mount them in a memory router. */
export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    children: [
  {
    // Open to everybody, signed in or not.
    element: <PublicLayout />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/news', element: <PublicNewsListPage /> },
      { path: '/news/:slug', element: <PublicNewsArticlePage /> },
      { path: '/announcements/public/:id', element: <PublicAnnouncementPage /> },
    ],
  },
  { path: '/login', element: <LoginPage /> },
  { path: '/auth/callback', element: <CallbackPage /> },
  {
    // Everything below needs a signed-in user; each page then narrows it to the roles that may open it.
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { path: '/dashboard', element: guarded(APP_ROLES, <DashboardPage />) },
          { path: '/announcements', element: guarded(APP_ROLES, <AnnouncementsInboxPage />) },
          { path: '/announcements/:id', element: guarded(APP_ROLES, <AnnouncementDetailPage />) },

          { path: '/manage/students', element: guarded(STAFF, <StudentsPage />) },
          { path: '/manage/teachers', element: guarded(EDITORS, <TeachersPage />) },
          { path: '/manage/classrooms', element: guarded(STAFF, <ClassroomsPage />) },
          { path: '/manage/subjects', element: guarded(STAFF, <SubjectsPage />) },
          { path: '/manage/academic-years', element: guarded(STAFF, <AcademicYearsPage />) },
          { path: '/manage/academic-years/rollover', element: guarded(ADMIN, <RolloverPage />) },

          { path: '/manage/news', element: guarded(EDITORS, <NewsManagePage />) },
          { path: '/manage/news/new', element: guarded(EDITORS, <NewsEditPage />) },
          { path: '/manage/news/:id', element: guarded(EDITORS, <NewsEditPage />) },
          { path: '/manage/announcements', element: guarded(EDITORS, <AnnouncementsManagePage />) },
          { path: '/manage/announcements/new', element: guarded(EDITORS, <AnnouncementEditPage />) },
          { path: '/manage/announcements/:id', element: guarded(EDITORS, <AnnouncementEditPage />) },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
    ],
  },
]

export const createAppRouter = () => createBrowserRouter(routes)
