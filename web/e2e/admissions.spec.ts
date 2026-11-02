import { expect as baseExpect, test, type APIRequestContext, type Page } from '@playwright/test'
import { adminApi, loginAs, tinyPng, unique } from './support/helpers.ts'

const keycloak = process.env.KEYCLOAK_URL ?? 'http://localhost:8080'
const realm = 'schomans'

// The first calls after a sign-in or after the API starts are slow (the account is created, the first query compiles), so
// the default 5 seconds of an assertion is too tight for these long stories.
const expect = baseExpect.configure({ timeout: 20_000 })

// Phase 5 exit criterion: the admission flow passes end to end. A new person registers on the real Keycloak page, applies,
// uploads the documents to the real storage and hands in; the admin staff verify and accept; the decision stays hidden until
// the announcement moment (the test moves the dates of the period); the applicant confirms the place; the admin staff enroll
// them, which changes the realm role in the real Keycloak, so the next sign-in shows the student pages.
test.describe('Admissions', () => {
  const email = `e2e-adm-${unique()}@schomans.demo`
  const password = 'Reg1ster!Me'
  const nisn = `7${unique().slice(-9)}`

  interface Period {
    id: string
    name: string
    opensAt: string
    closesAt: string
    resultsAnnouncedAt: string
    reregistrationDeadline: string
  }

  const days = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString()

  async function openPeriod(request: APIRequestContext): Promise<Period> {
    const api = await adminApi(request)
    const periods = await api.get<Period[]>('/api/v1/admissions/periods')
    const now = Date.now()
    const open = periods.find((p) => Date.parse(p.opensAt) <= now && now < Date.parse(p.closesAt))
    expect(open, 'the demo data has an admission period that is open').toBeDefined()
    return open as Period
  }

  async function moveDates(request: APIRequestContext, period: Period, dates: Pick<Period, 'opensAt' | 'closesAt' | 'resultsAnnouncedAt' | 'reregistrationDeadline'>) {
    const api = await adminApi(request)
    const response = await api.put(`/api/v1/admissions/periods/${period.id}`, { ...dates, name: period.name })
    expect(response.status).toBe(200)
  }

  async function adminToken(request: APIRequestContext): Promise<string> {
    const response = await request.post(`${keycloak}/realms/master/protocol/openid-connect/token`, {
      form: {
        grant_type: 'password',
        client_id: 'admin-cli',
        username: process.env.KEYCLOAK_ADMIN ?? 'admin',
        password: process.env.KEYCLOAK_ADMIN_PASSWORD ?? 'admin',
      },
    })
    return ((await response.json()) as { access_token?: string }).access_token ?? ''
  }

  async function signInOnKeycloak(page: Page) {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in with your account' }).click()
    await page.locator('#username').fill(email)
    await page.locator('#password').fill(password)
    await page.locator('#kc-login').click()
    await expect(page).toHaveURL(/localhost:5173\/dashboard$/)
  }

  test.afterAll(async ({ request }) => {
    // Leave Keycloak, the student and the dates of the demo period as they were. The application itself stays (there is no way to delete one).
    const token = await adminToken(request)
    const headers = { Authorization: `Bearer ${token}` }
    const students = await (await adminApi(request)).get<{ items: { id: string; email: string }[] }>(`/api/v1/students?search=${nisn}`)
    for (const student of students.items.filter((s) => s.email === email)) {
      await (await adminApi(request)).del(`/api/v1/students/${student.id}`)
    }

    const users = await request.get(`${keycloak}/admin/realms/${realm}/users`, { params: { email, exact: 'true' }, headers })
    for (const user of ((await users.json().catch(() => [])) as { id: string }[]) ?? []) {
      await request.delete(`${keycloak}/admin/realms/${realm}/users/${user.id}`, { headers })
    }
  })

  test('an applicant is accepted, confirms the place and is enrolled as a student', async ({ browser, request }) => {
    test.setTimeout(300_000)
    const original = await openPeriod(request)
    const applicantContext = await browser.newContext()
    const adminContext = await browser.newContext()

    try {
      const applicant = await applicantContext.newPage()
      const admin = await adminContext.newPage()

      // 1. Registration on the Keycloak page and the application: the form, three documents, handing in.
      await applicant.goto('/login')
      await applicant.getByRole('button', { name: 'Sign in with your account' }).click()
      await applicant.getByRole('link', { name: 'Register' }).click()
      await applicant.locator('#firstName').fill('Gita')
      await applicant.locator('#lastName').fill('Pratama')
      await applicant.locator('#email').fill(email)
      await applicant.locator('#password').fill(password)
      await applicant.locator('#password-confirm').fill(password)
      await applicant.getByRole('button', { name: 'Register' }).click()
      await expect(applicant).toHaveURL(/localhost:5173\/dashboard$/)
      await expect(applicant.getByRole('heading', { level: 1 })).toHaveText('Applicant dashboard')

      await applicant.getByRole('link', { name: 'My application' }).first().click()
      await applicant.getByRole('button', { name: 'Start application' }).click()
      await expect(applicant.getByLabel('Full name')).toHaveValue('Gita Pratama')

      await applicant.getByLabel('Date of birth').fill('2013-05-17')
      await applicant.getByLabel('Place of birth').fill('Bandung')
      await applicant.getByLabel('Address').fill('Jl. Merdeka 1, Bandung')
      await applicant.getByLabel(/^Parent or guardian/).fill('Agus Pratama')
      await applicant.getByLabel('Phone number of the parent or guardian').fill('+62 812-3456-7890')
      await applicant.getByLabel('Previous school').fill('SD Negeri 1 Bandung')
      await applicant.getByRole('button', { name: 'Save draft' }).click()
      await expect(applicant.getByText('Draft saved').first()).toBeVisible()

      // A file of the wrong type is refused before anything is uploaded.
      await applicant.getByLabel('Birth certificate: choose a file').setInputFiles({ name: 'virus.exe', mimeType: 'application/x-msdownload', buffer: Buffer.from('MZ') })
      await expect(applicant.getByText('Upload a PDF or an image (JPEG, PNG, WebP).').first()).toBeVisible()

      for (const [label, name] of [
        ['Birth certificate', 'akta-lahir.png'],
        ['Family card', 'kartu-keluarga.png'],
        ['Latest report card', 'rapor.png'],
      ] as const) {
        await applicant.getByLabel(`${label}: choose a file`).setInputFiles({ name, mimeType: 'image/png', buffer: tinyPng })
        // The button of that document turns into "Upload again" when the file is attached (a toast of the one before may still show).
        await expect(applicant.getByRole('button', { name: new RegExp(`^Upload again.*${label}`) })).toBeVisible()
      }

      await applicant.screenshot({ path: 'test-results/screens/admissions-applicant-draft.png', fullPage: true })
      await applicant.getByRole('button', { name: 'Hand in the application' }).click()
      await applicant.getByRole('alertdialog').getByRole('button', { name: 'Hand in', exact: true }).click()
      await expect(applicant.getByText('Application handed in').first()).toBeVisible()
      await expect(applicant.getByText('Handed in', { exact: true }).first()).toBeVisible()

      // 2. The admin staff verify the three documents and accept.
      await loginAs(admin, 'Admin staff')
      await admin.getByRole('link', { name: 'Admissions', exact: true }).first().click()
      await admin.getByLabel('Search by name or email').fill(email)
      await admin.getByRole('row', { name: new RegExp(email) }).getByRole('link', { name: 'Open' }).click()
      await expect(admin.getByRole('heading', { level: 1 })).toBeVisible()

      for (let i = 0; i < 3; i++) {
        await admin.getByRole('button', { name: /^Verify/ }).nth(i).click()
        await expect(admin.getByText('Document verified').first()).toBeVisible()
      }

      await admin.getByRole('button', { name: 'Accept', exact: true }).click()
      await admin.getByRole('alertdialog').getByRole('button', { name: 'Accept', exact: true }).click()
      await expect(admin.getByText('Decision saved').first()).toBeVisible()
      await admin.screenshot({ path: 'test-results/screens/admissions-staff-detail.png', fullPage: true })

      // 3. Until the announcement the applicant sees "in verification", nothing more.
      await applicant.reload()
      await expect(applicant.getByText('In verification', { exact: true }).first()).toBeVisible()
      await expect(applicant.getByText(/Congratulations, you are accepted/)).toHaveCount(0)
      await expect(applicant.getByRole('button', { name: 'Confirm my place' })).toHaveCount(0)

      // 4. The announcement moment comes: the result shows, and the applicant confirms the place.
      await moveDates(request, original, { opensAt: days(-10), closesAt: days(-3), resultsAnnouncedAt: days(-1), reregistrationDeadline: days(10) })
      await applicant.reload()
      await expect(applicant.getByText('Accepted', { exact: true }).first()).toBeVisible()
      await expect(applicant.getByText(/Congratulations, you are accepted/)).toBeVisible()
      await applicant.screenshot({ path: 'test-results/screens/admissions-applicant-accepted.png', fullPage: true })
      await applicant.getByRole('button', { name: 'Confirm my place' }).click()
      await expect(applicant.getByText('Your place is confirmed').first()).toBeVisible()

      // 5. The admin staff enroll the applicant; the role changes in Keycloak.
      // The demo session lives in memory, so the admin goes back to the application through the list, not with a reload.
      await admin.getByRole('link', { name: 'Admissions', exact: true }).first().click()
      await admin.getByLabel('Search by name or email').fill(email)
      await admin.getByRole('row', { name: new RegExp(email) }).getByRole('link', { name: 'Open' }).click()
      await admin.getByLabel('Classroom').selectOption({ label: 'Class A' })
      await admin.getByLabel('NISN').fill(nisn)
      await admin.getByRole('button', { name: 'Enroll', exact: true }).click()
      await expect(admin.getByText('Applicant enrolled as a student').first()).toBeVisible()
      await admin.screenshot({ path: 'test-results/screens/admissions-staff-enrolled.png', fullPage: true })

      // 6. A new sign-in carries the new realm role: the pages of a student.
      await applicant.getByRole('button', { name: 'Sign out' }).click()
      await expect(applicant).toHaveURL(/localhost:5173\/login$/)
      await signInOnKeycloak(applicant)
      await expect(applicant.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')
      await applicant.screenshot({ path: 'test-results/screens/admissions-now-a-student.png', fullPage: true })
    } finally {
      await moveDates(request, original, {
        opensAt: original.opensAt,
        closesAt: original.closesAt,
        resultsAnnouncedAt: original.resultsAnnouncedAt,
        reregistrationDeadline: original.reregistrationDeadline,
      })
      await Promise.all([applicantContext.close(), adminContext.close()])
    }
  })

  test('the landing page announces the open admission period to guests', async ({ page, request }) => {
    const period = await openPeriod(request)
    await page.goto('/')

    await expect(page.getByRole('heading', { name: period.name })).toBeVisible()
    await expect(page.getByText(/Applications are open until/)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Apply now' })).toBeVisible()
    await page.screenshot({ path: 'test-results/screens/landing-admissions-banner.png', fullPage: true })
  })

  test('an admin cannot open a second period while one is open', async ({ page }) => {
    // A value for a datetime-local input: the school's wall clock, which is what the form means.
    const local = (offset: number) => {
      const date = new Date(Date.now() + offset * 86_400_000 + 7 * 3_600_000)
      return date.toISOString().slice(0, 16)
    }

    await loginAs(page, 'Admin staff')
    await page.getByRole('link', { name: 'Admission periods', exact: true }).first().click()
    await expect(page.getByRole('heading', { level: 1, name: 'Admission periods' })).toBeVisible()
    await expect(page.getByRole('row').filter({ hasText: 'Intake' }).first()).toBeVisible()

    await page.getByRole('button', { name: 'New period' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Name').fill('E2E overlapping intake')
    await dialog.getByLabel('Academic year').selectOption({ index: 1 })
    await dialog.getByLabel('Applications open').fill(local(1))
    await dialog.getByLabel('Applications close').fill(local(5))
    await dialog.getByLabel('Results announced').fill(local(8))
    await dialog.getByLabel('Re-registration deadline').fill(local(20))
    await page.screenshot({ path: 'test-results/screens/admissions-period-form.png' })
    await dialog.getByRole('button', { name: 'Save' }).click()

    await expect(page.getByText('Another admission period is open at the same time.').first()).toBeVisible()
  })
})
