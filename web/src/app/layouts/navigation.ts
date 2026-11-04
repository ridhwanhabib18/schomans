import {
  BarChart3,
  Bot,
  BookOpen,
  CalendarClock,
  CalendarRange,
  ClipboardCheck,
  ClipboardList,
  FileText,
  Inbox,
  Mail,
  ListChecks,
  ShieldCheck,
  GraduationCap,
  LayoutDashboard,
  Award,
  Megaphone,
  Newspaper,
  NotebookPen,
  UserPlus,
  Workflow,
  ScanLine,
  School,
  Sparkles,
  StickyNote,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { APP_ROLES, type AppRole } from '@/shared/lib/roles'

export type NavSection = 'main' | 'classroom' | 'children' | 'school' | 'communication' | 'manage'

export interface NavItem {
  to: string
  /** Key in the `common` namespace. */
  labelKey: string
  icon: LucideIcon
  roles: readonly AppRole[]
  section: NavSection
  /** Active only on this exact address, not on the pages below it. */
  end?: boolean
}

const STAFF: readonly AppRole[] = ['Admin', 'Principal', 'Teacher']
const EDITORS: readonly AppRole[] = ['Admin', 'Principal']

/**
 * The sidebar (desktop) and bottom navigation (phone). Each module adds its own entries as it is built. The roles here
 * only decide what is shown; the API and the route guards enforce the same rules.
 */
export const navItems: readonly NavItem[] = [
  { to: '/dashboard', labelKey: 'nav.dashboard', icon: LayoutDashboard, roles: APP_ROLES, section: 'main' },
  { to: '/assistant', labelKey: 'nav.assistant', icon: Bot, roles: ['Principal'], section: 'main' },
  { to: '/admissions', labelKey: 'nav.myApplication', icon: ClipboardList, roles: ['Applicant'], section: 'main' },
  { to: '/attendance', labelKey: 'nav.attendance', icon: ClipboardCheck, roles: ['Teacher'], section: 'classroom', end: true },
  { to: '/attendance/scan', labelKey: 'nav.scanQr', icon: ScanLine, roles: ['Student'], section: 'classroom' },
  { to: '/attendance/me', labelKey: 'nav.myAttendance', icon: ClipboardCheck, roles: ['Student'], section: 'classroom' },
  { to: '/assignments', labelKey: 'nav.assignments', icon: NotebookPen, roles: ['Teacher', 'Principal', 'Student'], section: 'classroom' },
  { to: '/grades/me', labelKey: 'nav.myGrades', icon: Award, roles: ['Student'], section: 'classroom' },
  { to: '/grading', labelKey: 'nav.gradeBook', icon: Award, roles: ['Teacher'], section: 'classroom' },
  { to: '/progress-summaries', labelKey: 'nav.progressSummaries', icon: Sparkles, roles: ['Teacher'], section: 'classroom' },
  { to: '/teacher-notes', labelKey: 'nav.teacherNotes', icon: StickyNote, roles: ['Teacher'], section: 'classroom' },
  { to: '/attendance/reports', labelKey: 'nav.attendanceReports', icon: BarChart3, roles: STAFF, section: 'classroom' },
  { to: '/my-children/attendance', labelKey: 'nav.childAttendance', icon: ClipboardCheck, roles: ['Parent'], section: 'children' },
  { to: '/my-children/assignments', labelKey: 'nav.childAssignments', icon: NotebookPen, roles: ['Parent'], section: 'children' },
  { to: '/my-children/grades', labelKey: 'nav.childGrades', icon: Award, roles: ['Parent'], section: 'children' },
  { to: '/my-children/summaries', labelKey: 'nav.childSummaries', icon: Sparkles, roles: ['Parent'], section: 'children' },
  { to: '/announcements', labelKey: 'nav.announcements', icon: Megaphone, roles: APP_ROLES, section: 'communication' },
  { to: '/manage/students', labelKey: 'nav.students', icon: GraduationCap, roles: STAFF, section: 'school' },
  { to: '/manage/teachers', labelKey: 'nav.teachers', icon: Users, roles: EDITORS, section: 'school' },
  { to: '/manage/classrooms', labelKey: 'nav.classrooms', icon: School, roles: STAFF, section: 'school' },
  { to: '/manage/subjects', labelKey: 'nav.subjects', icon: BookOpen, roles: STAFF, section: 'school' },
  { to: '/manage/academic-years', labelKey: 'nav.academicYears', icon: CalendarRange, roles: STAFF, section: 'school' },
  { to: '/manage/schedules', labelKey: 'nav.timetable', icon: CalendarClock, roles: STAFF, section: 'school' },
  { to: '/manage/grade-bands', labelKey: 'nav.gradeBands', icon: Award, roles: ['Admin'], section: 'school' },
  { to: '/manage/admissions', labelKey: 'nav.admissions', icon: UserPlus, roles: ['Admin'], section: 'school' },
  { to: '/manage/admission-periods', labelKey: 'nav.admissionPeriods', icon: CalendarRange, roles: ['Admin'], section: 'school' },
  { to: '/requests', labelKey: 'nav.requests', icon: FileText, roles: ['Parent', 'Student', 'Teacher'], section: 'communication', end: true },
  { to: '/requests/inbox', labelKey: 'nav.approvals', icon: Inbox, roles: STAFF, section: 'communication' },
  { to: '/manage/requests', labelKey: 'nav.allRequests', icon: ListChecks, roles: EDITORS, section: 'manage' },
  { to: '/manage/letters', labelKey: 'nav.generalLetters', icon: Mail, roles: EDITORS, section: 'manage' },
  { to: '/manage/audit-log', labelKey: 'nav.auditLog', icon: ShieldCheck, roles: EDITORS, section: 'manage' },
  { to: '/manage/approval-chains', labelKey: 'nav.approvalChains', icon: Workflow, roles: EDITORS, section: 'manage' },
  { to: '/manage/news', labelKey: 'nav.manageNews', icon: Newspaper, roles: EDITORS, section: 'manage' },
  { to: '/manage/announcements', labelKey: 'nav.manageAnnouncements', icon: Megaphone, roles: EDITORS, section: 'manage' },
]

const SECTION_ORDER: readonly NavSection[] = ['main', 'classroom', 'children', 'school', 'communication', 'manage']

export function navItemsFor(role: AppRole | null): NavItem[] {
  return role ? navItems.filter((item) => item.roles.includes(role)) : []
}

/** The items of a role in display order, grouped by section; an empty section is left out. */
export function navSectionsFor(role: AppRole | null): { section: NavSection; items: NavItem[] }[] {
  const items = navItemsFor(role)
  return SECTION_ORDER.map((section) => ({ section, items: items.filter((item) => item.section === section) })).filter((group) => group.items.length > 0)
}
