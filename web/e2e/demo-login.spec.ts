import { expect, test } from '@playwright/test'

// The six demo accounts of the seeded fictional school (infra/keycloak/realm-schomans.json).
const demoUsers = [
  { button: 'Principal', name: 'Hendra Wirawan', role: 'Principal' },
  { button: 'Teacher', name: 'Budi Santoso', role: 'Teacher' },
  { button: 'Student', name: 'Rina Putri', role: 'Student' },
  { button: 'Parent', name: 'Agus Putra', role: 'Parent' },
  { button: 'Admin staff', name: 'Sari Utami', role: 'Admin' },
  { button: 'Applicant', name: 'Dewi Lestari', role: 'Applicant' },
] as const

test.describe('demo login: every role sees its own empty dashboard', () => {
  for (const user of demoUsers) {
    test(`${user.role}`, async ({ page }) => {
      await page.goto('/login')
      await expect(page.getByRole('heading', { name: 'Try the demo' })).toBeVisible()

      await page.getByRole('button', { name: new RegExp(`^${user.button}`) }).click()

      await expect(page).toHaveURL(/\/dashboard$/)
      await expect(page.getByRole('heading', { level: 1 })).toHaveText(`${user.role} dashboard`)
      await expect(page.getByText(`Welcome, ${user.name}`)).toBeVisible()
      await expect(page.getByText('Nothing to show yet')).toBeVisible()
      await expect(page.getByRole('banner').getByText(user.name)).toBeVisible()
      await page.screenshot({ path: `test-results/screens/dashboard-${user.role.toLowerCase()}.png`, fullPage: true })
    })
  }

  test('signing out returns to the login page and protects the dashboard again', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: /^Teacher/ }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    await page.getByRole('button', { name: 'Sign out' }).click()

    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByRole('heading', { name: 'Sign in to SchoMans' })).toBeVisible()
  })
})

test.describe('guests and protected pages', () => {
  test('an anonymous visitor who opens the dashboard is sent to the login page', async ({ page }) => {
    await page.goto('/dashboard')

    await expect(page).toHaveURL(/\/login$/)
  })

  test('a guest can browse the public page without an account', async ({ page }) => {
    await page.goto('/login')

    await page.getByRole('link', { name: 'Browse as guest' }).click()

    await expect(page).toHaveURL(/localhost:5173\/$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('School management in one place')
  })

  test('an unknown address shows the not-found page', async ({ page }) => {
    await page.goto('/this/does/not/exist')

    await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible()
  })

  test('a demo user opening the login page again is taken to the dashboard', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: /^Student/ }).click()
    await expect(page).toHaveURL(/\/dashboard$/)

    // Navigating inside the app keeps the in-memory session (a full reload would not: tokens are never stored).
    await page.getByRole('link', { name: 'Dashboard' }).first().click()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')
  })
})
