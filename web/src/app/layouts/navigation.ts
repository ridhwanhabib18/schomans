import { LayoutDashboard, type LucideIcon } from 'lucide-react'
import { APP_ROLES, type AppRole } from '@/shared/lib/roles'

export interface NavItem {
  to: string
  /** Key in the `common` namespace. */
  labelKey: string
  icon: LucideIcon
  roles: readonly AppRole[]
}

/** The sidebar (desktop) and bottom navigation (phone). Each module adds its own entry as it is built. */
export const navItems: readonly NavItem[] = [
  { to: '/dashboard', labelKey: 'nav.dashboard', icon: LayoutDashboard, roles: APP_ROLES },
]

export function navItemsFor(role: AppRole | null): NavItem[] {
  return role ? navItems.filter((item) => item.roles.includes(role)) : []
}
