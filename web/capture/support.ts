import { expect, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import path from 'node:path'

/** The label of a demo login button, and the folder its pictures go to (English, kebab-case). */
export const roles = [
  { button: 'Principal', folder: 'principal' },
  { button: 'Admin staff', folder: 'admin-staff' },
  { button: 'Teacher', folder: 'teacher' },
  { button: 'Student', folder: 'student' },
  { button: 'Parent', folder: 'parent' },
  { button: 'Applicant', folder: 'applicant' },
] as const

export const sizes = {
  desktop: { width: 1440, height: 900 },
  mobile: { width: 390, height: 844 },
} as const

export type Size = keyof typeof sizes

/** docs/assets relative to the web folder, where `npm run capture:screenshots` runs. */
export const assetsRoot = path.resolve(process.cwd(), '..', 'docs', 'assets')

/** Signs in with a demo button. The tokens live in memory, so the page must never be reloaded afterwards. */
export async function signIn(page: Page, button: string): Promise<void> {
  await page.goto('/login')
  await page.getByRole('button', { name: new RegExp(`^${button}`) }).click()
  await expect(page).not.toHaveURL(/\/login$/)
  await page.waitForLoadState('networkidle').catch(() => undefined)
}

/** Moves inside the app without a page load, so the session survives. */
export async function goInApp(page: Page, target: string): Promise<void> {
  await page.evaluate((to) => {
    window.history.pushState({}, '', to)
    window.dispatchEvent(new PopStateEvent('popstate'))
  }, target)
}

/** `/manage/students` becomes `manage-students`; an id in the path becomes `detail`. */
export function slugOf(route: string): string {
  const clean = route.split('?')[0]!.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/g, 'detail')
  return clean.replace(/^\/+|\/+$/g, '').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase() || 'home'
}

/** Waits until the page has settled: a heading, no pending requests, no skeletons. */
export async function settle(page: Page): Promise<void> {
  await page.getByRole('heading', { level: 1 }).first().waitFor({ state: 'visible', timeout: 20_000 }).catch(() => undefined)
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined)
  await page.waitForFunction(() => document.querySelectorAll('[class*="animate-pulse"]').length === 0, undefined, { timeout: 10_000 }).catch(() => undefined)
  await page.waitForTimeout(300)
}

export async function shoot(page: Page, folder: string, route: string, size: Size): Promise<string> {
  const dir = path.join(assetsRoot, 'screenshots', folder)
  mkdirSync(dir, { recursive: true })
  const file = path.join(dir, `${slugOf(route)}-${size}.png`)
  // A phone shows one screen with the bottom navigation pinned to it; a full-page picture would put that bar in the middle.
  await page.screenshot({ path: file, fullPage: size === 'desktop' })
  return file
}
