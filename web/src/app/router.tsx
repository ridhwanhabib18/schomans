import type { ReactNode } from 'react'
import { createBrowserRouter, type RouteObject } from 'react-router'
import { AssistantPage } from '@/features/assistant/pages/AssistantPage'
import { AssignmentDetailPage } from '@/features/assignments/pages/AssignmentDetailPage'
import { AssignmentEditorPage } from '@/features/assignments/pages/AssignmentEditorPage'
import { AssignmentsHomePage } from '@/features/assignments/pages/AssignmentsHomePage'
import { ChildAssignmentsPage } from '@/features/assignments/pages/StudentAssignmentsPage'
import { AttendanceReportPage } from '@/features/attendance/pages/AttendanceReportPage'
import { CheckInPage } from '@/features/attendance/pages/CheckInPage'
import { ChildAttendancePage, MyAttendancePage } from '@/features/attendance/pages/MyAttendancePage'
import { ScanPage } from '@/features/attendance/pages/ScanPage'
import { SessionPage } from '@/features/attendance/pages/SessionPage'
import { TodayLessonsPage } from '@/features/attendance/pages/TodayLessonsPage'
import { AdmissionPeriodsPage } from '@/features/admissions/pages/AdmissionPeriodsPage'
import { ApplicantDetailPage } from '@/features/admissions/pages/ApplicantDetailPage'
import { ApplicantsPage } from '@/features/admissions/pages/ApplicantsPage'
import { MyApplicationPage } from '@/features/admissions/pages/MyApplicationPage'
import { AnnouncementDetailPage } from '@/features/announcements/pages/AnnouncementDetailPage'
import { AnnouncementEditPage } from '@/features/announcements/pages/AnnouncementEditPage'
import { AnnouncementsInboxPage } from '@/features/announcements/pages/AnnouncementsInboxPage'
import { AnnouncementsManagePage } from '@/features/announcements/pages/AnnouncementsManagePage'
import { PublicAnnouncementPage } from '@/features/announcements/pages/PublicAnnouncementPage'
import { CallbackPage } from '@/features/auth/pages/CallbackPage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireRole } from '@/features/auth/components/RequireRole'
import { ChildSummariesPage } from '@/features/progress/pages/MySummariesPage'
import { ReviewSummariesPage } from '@/features/progress/pages/ReviewSummariesPage'
import { TeacherNotesPage } from '@/features/progress/pages/TeacherNotesPage'
import { ChildGradesPage, MyGradesPage } from '@/features/reportcards/pages/GradesPages'
import { ApprovalChainsPage } from '@/features/requests/pages/ApprovalChainsPage'
import { AllRequestsPage, MyRequestsPage, RequestInboxPage } from '@/features/requests/pages/ListPages'
import { NewRequestPage } from '@/features/requests/pages/NewRequestPage'
import { RequestDetailPage } from '@/features/requests/pages/RequestDetailPage'
import { VerifyLetterPage } from '@/features/requests/pages/VerifyLetterPage'
import { GradeBandsPage } from '@/features/grading/pages/GradeBandsPage'
import { GradeBookPage } from '@/features/grading/pages/GradeBookPage'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { AcademicYearsPage } from '@/features/masterdata/pages/AcademicYearsPage'
import { ClassroomsPage } from '@/features/masterdata/pages/ClassroomsPage'
import { RolloverPage } from '@/features/masterdata/pages/RolloverPage'
import { SchedulesPage } from '@/features/masterdata/pages/SchedulesPage'
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
const TEACHER: readonly AppRole[] = ['Teacher']
const STUDENT: readonly AppRole[] = ['Student']
const PARENT: readonly AppRole[] = ['Parent']
const PRINCIPAL: readonly AppRole[] = ['Principal']
const APPLICANT: readonly AppRole[] = ['Applicant']
// Admin staff do not work with assignments (authorization matrix).
const ASSIGNMENT_READERS: readonly AppRole[] = ['Teacher', 'Principal', 'Student', 'Parent']

// Requests: parents, students and teachers make them; staff and the principal oversee them (everybody inside the school except applicants).
const REQUEST_MAKERS: readonly AppRole[] = ['Parent', 'Student', 'Teacher']
const REQUEST_VIEWERS: readonly AppRole[] = ['Admin', 'Principal', 'Teacher', 'Parent', 'Student']

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
      { path: '/verify/:code', element: <VerifyLetterPage /> },
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
          { path: '/manage/schedules', element: guarded(STAFF, <SchedulesPage />) },

          { path: '/attendance', element: guarded(TEACHER, <TodayLessonsPage />) },
          { path: '/attendance/sessions/:id', element: guarded(STAFF, <SessionPage />) },
          { path: '/attendance/reports', element: guarded(STAFF, <AttendanceReportPage />) },
          { path: '/attendance/scan', element: guarded(STUDENT, <ScanPage />) },
          { path: '/attendance/me', element: guarded(STUDENT, <MyAttendancePage />) },
          { path: '/attend', element: guarded(STUDENT, <CheckInPage />) },
          { path: '/my-children/attendance', element: guarded(PARENT, <ChildAttendancePage />) },

          { path: '/assignments', element: guarded(ASSIGNMENT_READERS, <AssignmentsHomePage />) },
          { path: '/assignments/new', element: guarded(TEACHER, <AssignmentEditorPage />) },
          { path: '/assignments/:id', element: guarded(ASSIGNMENT_READERS, <AssignmentDetailPage />) },
          { path: '/assignments/:id/edit', element: guarded(TEACHER, <AssignmentEditorPage />) },
          { path: '/my-children/assignments', element: guarded(PARENT, <ChildAssignmentsPage />) },

          { path: '/grades/me', element: guarded(STUDENT, <MyGradesPage />) },
          { path: '/my-children/grades', element: guarded(PARENT, <ChildGradesPage />) },
          { path: '/grading', element: guarded(TEACHER, <GradeBookPage />) },
          { path: '/manage/grade-bands', element: guarded(ADMIN, <GradeBandsPage />) },

          { path: '/assistant', element: guarded(PRINCIPAL, <AssistantPage />) },

          { path: '/progress-summaries', element: guarded(TEACHER, <ReviewSummariesPage />) },
          { path: '/teacher-notes', element: guarded(TEACHER, <TeacherNotesPage />) },
          { path: '/my-children/summaries', element: guarded(PARENT, <ChildSummariesPage />) },

          { path: '/admissions', element: guarded(APPLICANT, <MyApplicationPage />) },
          { path: '/manage/admissions', element: guarded(ADMIN, <ApplicantsPage />) },
          { path: '/manage/admissions/:id', element: guarded(ADMIN, <ApplicantDetailPage />) },
          { path: '/manage/admission-periods', element: guarded(ADMIN, <AdmissionPeriodsPage />) },

          { path: '/requests', element: guarded(REQUEST_MAKERS, <MyRequestsPage />) },
          { path: '/requests/new', element: guarded(REQUEST_MAKERS, <NewRequestPage />) },
          { path: '/requests/inbox', element: guarded(STAFF, <RequestInboxPage />) },
          { path: '/requests/:id', element: guarded(REQUEST_VIEWERS, <RequestDetailPage />) },
          { path: '/manage/requests', element: guarded(EDITORS, <AllRequestsPage />) },
          { path: '/manage/approval-chains', element: guarded(EDITORS, <ApprovalChainsPage />) },

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
