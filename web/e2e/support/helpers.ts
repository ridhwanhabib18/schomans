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
    async post<T>(path: string, body: unknown): Promise<{ status: number; body: T }> {
      const response = await request.post(`${api}${path}`, { headers, data: body })
      return { status: response.status(), body: (await response.json().catch(() => null)) as T }
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

/** Moves inside the app without a page load, so the in-memory session of a demo login survives (a reload would lose it). */
export async function goInApp(page: Page, path: string): Promise<void> {
  await page.evaluate((target) => {
    window.history.pushState({}, '', target)
    window.dispatchEvent(new PopStateEvent('popstate'))
  }, path)
}

interface Named {
  id: string
  name: string
  homeroomTeacherId?: string
}

/**
 * A one-minute lesson today for the demo teacher in Class A (Mathematics), at a time nobody else uses, so a run never
 * collides with the demo timetable or with an earlier run. Returns where to find it on the teacher's screen.
 * The lesson stays in the demo data afterwards (attendance history points at it), which is fictional data.
 */
export async function createLessonToday(request: APIRequestContext): Promise<{ scheduleId: string; time: string }> {
  const api = await adminApi(request)
  const classrooms = await api.get<{ items: Named[] }>('/api/v1/classrooms?pageSize=100&search=Class A')
  const subjects = await api.get<{ items: Named[] }>('/api/v1/subjects?pageSize=100&search=Mathematics')
  const classA = classrooms.items.find((c) => c.name === 'Class A')!
  const math = subjects.items.find((s) => s.name === 'Mathematics')!
  const weekday = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Jakarta', weekday: 'long' }).format(new Date())

  for (let attempt = 0; attempt < 20; attempt++) {
    // Between 15:00 and 23:58 in the evening, a minute at random; an overlap is answered with a clash and tried again.
    const minutes = 15 * 60 + Math.floor(Math.random() * (8 * 60 + 58))
    const at = (m: number) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:00`
    const created = await api.post<{ id: string }>('/api/v1/schedules', {
      classroomId: classA.id,
      subjectId: math.id,
      teacherId: classA.homeroomTeacherId,
      dayOfWeek: weekday,
      startTime: at(minutes),
      endTime: at(minutes + 1),
    })
    if (created.status === 201) {
      return { scheduleId: created.body.id, time: at(minutes).slice(0, 5) }
    }
  }

  throw new Error('No free minute for a test lesson')
}
