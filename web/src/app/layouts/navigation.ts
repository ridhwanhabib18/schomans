import { BookOpen, CalendarRange, GraduationCap, LayoutDashboard, Megaphone, Newspaper, School, Users, type LucideIcon } from 'lucide-react'
import { APP_ROLES, type AppRole } from '@/shared/lib/roles'

export type NavSection = 'main' | 'school' | 'communication' | 'manage'

export interface NavItem {
  to: string
  /** Key in the `common` namespace. */
  labelKey: string
  icon: LucideIcon
  roles: readonly AppRole[]
  section: NavSection
}

const STAFF: readonly AppRole[] = ['Admin', 'Principal', 'Teacher']
const EDITORS: readonly AppRole[] = ['Admin', 'Principal']

/**
 * The sidebar (desktop) and bottom navigation (phone). Each module adds its own entries as it is built. The roles here
 * only decide what is shown; the API and the route guards enforce the same rules.
 */
export const navItems: readonly NavItem[] = [
  { to: '/dashboard', labelKey: 'nav.dashboard', icon: LayoutDashboard, roles: APP_ROLES, section: 'main' },
  { to: '/announcements', labelKey: 'nav.announcements', icon: Megaphone, roles: APP_ROLES, section: 'communication' },
  { to: '/manage/students', labelKey: 'nav.students', icon: GraduationCap, roles: STAFF, section: 'school' },
  { to: '/manage/teachers', labelKey: 'nav.teachers', icon: Users, roles: EDITORS, section: 'school' },
  { to: '/manage/classrooms', labelKey: 'nav.classrooms', icon: School, roles: STAFF, section: 'school' },
  { to: '/manage/subjects', labelKey: 'nav.subjects', icon: BookOpen, roles: STAFF, section: 'school' },
  { to: '/manage/academic-years', labelKey: 'nav.academicYears', icon: CalendarRange, roles: STAFF, section: 'school' },
  { to: '/manage/news', labelKey: 'nav.manageNews', icon: Newspaper, roles: EDITORS, section: 'manage' },
  { to: '/manage/announcements', labelKey: 'nav.manageAnnouncements', icon: Megaphone, roles: EDITORS, section: 'manage' },
]

const SECTION_ORDER: readonly NavSection[] = ['main', 'school', 'communication', 'manage']

export function navItemsFor(role: AppRole | null): NavItem[] {
  return role ? navItems.filter((item) => item.roles.includes(role)) : []
}

/** The items of a role in display order, grouped by section; an empty section is left out. */
export function navSectionsFor(role: AppRole | null): { section: NavSection; items: NavItem[] }[] {
  const items = navItemsFor(role)
  return SECTION_ORDER.map((section) => ({ section, items: items.filter((item) => item.section === section) })).filter((group) => group.items.length > 0)
}
