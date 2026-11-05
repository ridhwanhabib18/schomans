import { createBrowserRouter, type RouteObject } from 'react-router'
import { PublicAnnouncementPage } from '@/features/announcements/pages/PublicAnnouncementPage'
import { CallbackPage } from '@/features/auth/pages/CallbackPage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireRole } from '@/features/auth/components/RequireRole'
import { VerifyLetterPage } from '@/features/requests/pages/VerifyLetterPage'
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

/** Loads a page when its route is opened, so the public pages do not carry the code of the whole app. */
const pages = {
  AcademicYearsPage: () => import('@/features/masterdata/pages/AcademicYearsPage').then((module) => module.AcademicYearsPage),
  AdmissionPeriodsPage: () => import('@/features/admissions/pages/AdmissionPeriodsPage').then((module) => module.AdmissionPeriodsPage),
  AllRequestsPage: () => import('@/features/requests/pages/ListPages').then((module) => module.AllRequestsPage),
  AnnouncementDetailPage: () => import('@/features/announcements/pages/AnnouncementDetailPage').then((module) => module.AnnouncementDetailPage),
  AnnouncementEditPage: () => import('@/features/announcements/pages/AnnouncementEditPage').then((module) => module.AnnouncementEditPage),
  AnnouncementsInboxPage: () => import('@/features/announcements/pages/AnnouncementsInboxPage').then((module) => module.AnnouncementsInboxPage),
  AnnouncementsManagePage: () => import('@/features/announcements/pages/AnnouncementsManagePage').then((module) => module.AnnouncementsManagePage),
  ApplicantDetailPage: () => import('@/features/admissions/pages/ApplicantDetailPage').then((module) => module.ApplicantDetailPage),
  ApplicantsPage: () => import('@/features/admissions/pages/ApplicantsPage').then((module) => module.ApplicantsPage),
  ApprovalChainsPage: () => import('@/features/requests/pages/ApprovalChainsPage').then((module) => module.ApprovalChainsPage),
  AssignmentDetailPage: () => import('@/features/assignments/pages/AssignmentDetailPage').then((module) => module.AssignmentDetailPage),
  AssignmentEditorPage: () => import('@/features/assignments/pages/AssignmentEditorPage').then((module) => module.AssignmentEditorPage),
  AssignmentsHomePage: () => import('@/features/assignments/pages/AssignmentsHomePage').then((module) => module.AssignmentsHomePage),
  AssistantPage: () => import('@/features/assistant/pages/AssistantPage').then((module) => module.AssistantPage),
  AttendanceReportPage: () => import('@/features/attendance/pages/AttendanceReportPage').then((module) => module.AttendanceReportPage),
  AuditLogPage: () => import('@/features/audit/pages/AuditLogPage').then((module) => module.AuditLogPage),
  CheckInPage: () => import('@/features/attendance/pages/CheckInPage').then((module) => module.CheckInPage),
  ChildAssignmentsPage: () => import('@/features/assignments/pages/StudentAssignmentsPage').then((module) => module.ChildAssignmentsPage),
  ChildAttendancePage: () => import('@/features/attendance/pages/MyAttendancePage').then((module) => module.ChildAttendancePage),
  ChildGradesPage: () => import('@/features/reportcards/pages/GradesPages').then((module) => module.ChildGradesPage),
  ChildSummariesPage: () => import('@/features/progress/pages/MySummariesPage').then((module) => module.ChildSummariesPage),
  ClassroomsPage: () => import('@/features/masterdata/pages/ClassroomsPage').then((module) => module.ClassroomsPage),
  DashboardPage: () => import('@/features/dashboard/pages/DashboardPage').then((module) => module.DashboardPage),
  GeneralLettersPage: () => import('@/features/letters/pages/GeneralLettersPage').then((module) => module.GeneralLettersPage),
  GradeBandsPage: () => import('@/features/grading/pages/GradeBandsPage').then((module) => module.GradeBandsPage),
  GradeBookPage: () => import('@/features/grading/pages/GradeBookPage').then((module) => module.GradeBookPage),
  MyApplicationPage: () => import('@/features/admissions/pages/MyApplicationPage').then((module) => module.MyApplicationPage),
  MyAttendancePage: () => import('@/features/attendance/pages/MyAttendancePage').then((module) => module.MyAttendancePage),
  MyGradesPage: () => import('@/features/reportcards/pages/GradesPages').then((module) => module.MyGradesPage),
  MyRequestsPage: () => import('@/features/requests/pages/ListPages').then((module) => module.MyRequestsPage),
  NewRequestPage: () => import('@/features/requests/pages/NewRequestPage').then((module) => module.NewRequestPage),
  NewsEditPage: () => import('@/features/news/pages/NewsEditPage').then((module) => module.NewsEditPage),
  NewsManagePage: () => import('@/features/news/pages/NewsManagePage').then((module) => module.NewsManagePage),
  QuestionBankPage: () => import('@/features/assignments/pages/QuestionBankPage').then((module) => module.QuestionBankPage),
  QuizBuilderPage: () => import('@/features/assignments/pages/QuizBuilderPage').then((module) => module.QuizBuilderPage),
  RequestDetailPage: () => import('@/features/requests/pages/RequestDetailPage').then((module) => module.RequestDetailPage),
  RequestInboxPage: () => import('@/features/requests/pages/ListPages').then((module) => module.RequestInboxPage),
  ReviewSummariesPage: () => import('@/features/progress/pages/ReviewSummariesPage').then((module) => module.ReviewSummariesPage),
  RolloverPage: () => import('@/features/masterdata/pages/RolloverPage').then((module) => module.RolloverPage),
  ScanPage: () => import('@/features/attendance/pages/ScanPage').then((module) => module.ScanPage),
  SchedulesPage: () => import('@/features/masterdata/pages/SchedulesPage').then((module) => module.SchedulesPage),
  SessionPage: () => import('@/features/attendance/pages/SessionPage').then((module) => module.SessionPage),
  StudentsPage: () => import('@/features/masterdata/pages/StudentsPage').then((module) => module.StudentsPage),
  SubjectsPage: () => import('@/features/masterdata/pages/SubjectsPage').then((module) => module.SubjectsPage),
  TeacherNotesPage: () => import('@/features/progress/pages/TeacherNotesPage').then((module) => module.TeacherNotesPage),
  TeachersPage: () => import('@/features/masterdata/pages/TeachersPage').then((module) => module.TeachersPage),
  TodayLessonsPage: () => import('@/features/attendance/pages/TodayLessonsPage').then((module) => module.TodayLessonsPage),
}

/** A route of the signed-in area: its page is fetched on first use, and only the roles that may open it get in. */
const page = (roles: readonly AppRole[], name: keyof typeof pages) => ({
  lazy: async () => {
    const Page = await pages[name]()
    return { Component: () => <RequireRole roles={roles}><Page /></RequireRole> }
  },
})

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
          { path: '/dashboard', ...page(APP_ROLES, 'DashboardPage') },
          { path: '/announcements', ...page(APP_ROLES, 'AnnouncementsInboxPage') },
          { path: '/announcements/:id', ...page(APP_ROLES, 'AnnouncementDetailPage') },

          { path: '/manage/students', ...page(STAFF, 'StudentsPage') },
          { path: '/manage/teachers', ...page(EDITORS, 'TeachersPage') },
          { path: '/manage/classrooms', ...page(STAFF, 'ClassroomsPage') },
          { path: '/manage/subjects', ...page(STAFF, 'SubjectsPage') },
          { path: '/manage/academic-years', ...page(STAFF, 'AcademicYearsPage') },
          { path: '/manage/academic-years/rollover', ...page(ADMIN, 'RolloverPage') },
          { path: '/manage/schedules', ...page(STAFF, 'SchedulesPage') },

          { path: '/attendance', ...page(TEACHER, 'TodayLessonsPage') },
          { path: '/attendance/sessions/:id', ...page(STAFF, 'SessionPage') },
          { path: '/attendance/reports', ...page(STAFF, 'AttendanceReportPage') },
          { path: '/attendance/scan', ...page(STUDENT, 'ScanPage') },
          { path: '/attendance/me', ...page(STUDENT, 'MyAttendancePage') },
          { path: '/attend', ...page(STUDENT, 'CheckInPage') },
          { path: '/my-children/attendance', ...page(PARENT, 'ChildAttendancePage') },

          { path: '/assignments', ...page(ASSIGNMENT_READERS, 'AssignmentsHomePage') },
          { path: '/assignments/quizzes/new', ...page(TEACHER, 'QuizBuilderPage') },
          { path: '/assignments/questions', ...page(TEACHER, 'QuestionBankPage') },
          { path: '/assignments/new', ...page(TEACHER, 'AssignmentEditorPage') },
          { path: '/assignments/:id', ...page(ASSIGNMENT_READERS, 'AssignmentDetailPage') },
          { path: '/assignments/:id/edit', ...page(TEACHER, 'AssignmentEditorPage') },
          { path: '/my-children/assignments', ...page(PARENT, 'ChildAssignmentsPage') },

          { path: '/grades/me', ...page(STUDENT, 'MyGradesPage') },
          { path: '/my-children/grades', ...page(PARENT, 'ChildGradesPage') },
          { path: '/grading', ...page(TEACHER, 'GradeBookPage') },
          { path: '/manage/grade-bands', ...page(ADMIN, 'GradeBandsPage') },

          { path: '/assistant', ...page(PRINCIPAL, 'AssistantPage') },

          { path: '/progress-summaries', ...page(TEACHER, 'ReviewSummariesPage') },
          { path: '/teacher-notes', ...page(TEACHER, 'TeacherNotesPage') },
          { path: '/my-children/summaries', ...page(PARENT, 'ChildSummariesPage') },

          { path: '/admissions', ...page(APPLICANT, 'MyApplicationPage') },
          { path: '/manage/admissions', ...page(ADMIN, 'ApplicantsPage') },
          { path: '/manage/admissions/:id', ...page(ADMIN, 'ApplicantDetailPage') },
          { path: '/manage/admission-periods', ...page(ADMIN, 'AdmissionPeriodsPage') },

          { path: '/requests', ...page(REQUEST_MAKERS, 'MyRequestsPage') },
          { path: '/requests/new', ...page(REQUEST_MAKERS, 'NewRequestPage') },
          { path: '/requests/inbox', ...page(STAFF, 'RequestInboxPage') },
          { path: '/requests/:id', ...page(REQUEST_VIEWERS, 'RequestDetailPage') },
          { path: '/manage/requests', ...page(EDITORS, 'AllRequestsPage') },
          { path: '/manage/letters', ...page(EDITORS, 'GeneralLettersPage') },
          { path: '/manage/audit-log', ...page(EDITORS, 'AuditLogPage') },
          { path: '/manage/approval-chains', ...page(EDITORS, 'ApprovalChainsPage') },

          { path: '/manage/news', ...page(EDITORS, 'NewsManagePage') },
          { path: '/manage/news/new', ...page(EDITORS, 'NewsEditPage') },
          { path: '/manage/news/:id', ...page(EDITORS, 'NewsEditPage') },
          { path: '/manage/announcements', ...page(EDITORS, 'AnnouncementsManagePage') },
          { path: '/manage/announcements/new', ...page(EDITORS, 'AnnouncementEditPage') },
          { path: '/manage/announcements/:id', ...page(EDITORS, 'AnnouncementEditPage') },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
    ],
  },
]

export const createAppRouter = () => createBrowserRouter(routes)
