import { expect, type APIRequestContext, type Page } from '@playwright/test'

const keycloak = process.env.KEYCLOAK_URL ?? 'http://localhost:8080'
const api = process.env.E2E_API_URL ?? 'http://localhost:5080'

/** The label of a demo login button. */
export type DemoRole = 'Admin staff' | 'Principal' | 'Teacher' | 'Student' | 'Parent'

const emails: Record<DemoRole, string> = {
  'Admin staff': 'admin@schomans.demo',
  Principal: 'principal@schomans.demo',
  Teacher: 'teacher@schomans.demo',
  Student: 'student@schomans.demo',
  Parent: 'parent@schomans.demo',
}

/** Signs in with one of the demo buttons and waits for the dashboard. Tokens live in memory, so stay inside the app afterwards. */
export async function loginAs(page: Page, role: DemoRole): Promise<void> {
  await page.goto('/login')
  await page.getByRole('button', { name: new RegExp(`^${role}`) }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
}

/** A bearer token for a demo user, to prepare and clean up data without going through the screens. */
export async function tokenFor(request: APIRequestContext, role: DemoRole): Promise<string> {
  const response = await request.post(`${keycloak}/realms/schomans/protocol/openid-connect/token`, {
    form: { client_id: 'schomans-web-demo', grant_type: 'password', username: emails[role], password: 'Demo123!' },
  })
  expect(response.ok()).toBe(true)
  return ((await response.json()) as { access_token: string }).access_token
}

/** The API as the admin: `get`, `del` for fixtures and cleanup. */
export async function adminApi(request: APIRequestContext) {
  const headers = { Authorization: `Bearer ${await tokenFor(request, 'Admin staff')}` }
  return {
    async get<T>(path: string): Promise<T> {
      const response = await request.get(`${api}${path}`, { headers })
      expect(response.ok(), `GET ${path}`).toBe(true)
      return (await response.json()) as T
    },
    async del(path: string): Promise<void> {
      await request.delete(`${api}${path}`, { headers })
    },
  }
}

/** Something that is unique per run, so tests never collide with each other or with the demo data. */
export function unique(): string {
  return `${Date.now()}`
}

/** A 1x1 PNG, enough for the cover image upload. */
export const tinyPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==',
  'base64',
)

/** The number in the label of the notification bell ("Notifications, 3 unread"), or 0. */
export async function unreadCount(page: Page): Promise<number> {
  const label = await page.getByRole('button', { name: /^Notifications/ }).getAttribute('aria-label')
  return Number(/(\d+) unread/.exec(label ?? '')?.[1] ?? 0)
}
