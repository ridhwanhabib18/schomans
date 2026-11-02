# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: demo-login.spec.ts >> guests and protected pages >> an anonymous visitor who opens the dashboard is sent to the login page
- Location: e2e\demo-login.spec.ts:51:3

# Error details

```
Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/login$/
Received string:  "http://localhost:5173/dashboard"
Timeout: 5000ms

Call log:
  - Expect "toHaveURL" with timeout 5000ms
    7 × locator resolved to <html lang="en">…</html>
      - unexpected value "http://localhost:5173/dashboard"

```

```yaml
- status: Checking your session…
- status
```

# Test source

```ts
  1  | import { expect, test } from '@playwright/test'
  2  | 
  3  | // The six demo accounts of the seeded fictional school (infra/keycloak/realm-schomans.json).
  4  | const demoUsers = [
  5  |   { button: 'Principal', name: 'Hendra Wirawan', role: 'Principal', section: 'Attendance per class' },
  6  |   { button: 'Teacher', name: 'Budi Santoso', role: 'Teacher', section: 'Students who need attention' },
  7  |   { button: 'Student', name: 'Rina Putri', role: 'Student', section: /^Progress of / },
  8  |   { button: 'Parent', name: 'Agus Putra', role: 'Parent', section: /^Progress of / },
  9  |   // Admin staff and applicants have no analytics (F-09): their dashboard stays empty.
  10 |   { button: 'Admin staff', name: 'Sari Utami', role: 'Admin', section: null },
  11 |   { button: 'Applicant', name: 'Dewi Lestari', role: 'Applicant', section: null },
  12 | ] as const
  13 | 
  14 | test.describe('demo login: every role sees its own dashboard', () => {
  15 |   for (const user of demoUsers) {
  16 |     test(`${user.role}`, async ({ page }) => {
  17 |       await page.goto('/login')
  18 |       await expect(page.getByRole('heading', { name: 'Try the demo' })).toBeVisible()
  19 | 
  20 |       await page.getByRole('button', { name: new RegExp(`^${user.button}`) }).click()
  21 | 
  22 |       await expect(page).toHaveURL(/\/dashboard$/)
  23 |       await expect(page.getByRole('heading', { level: 1 })).toHaveText(`${user.role} dashboard`)
  24 |       await expect(page.getByText(`Welcome, ${user.name}`)).toBeVisible()
  25 |       if (user.section === null) {
  26 |         await expect(page.getByText('Nothing to show yet')).toBeVisible()
  27 |       } else {
  28 |         // The analytics of the role (F-09) replace the empty state.
  29 |         await expect(page.getByRole('heading', { name: user.section }).first()).toBeVisible()
  30 |         await expect(page.getByText('Nothing to show yet')).toHaveCount(0)
  31 |       }
  32 |       await expect(page.getByRole('banner').getByText(user.name)).toBeVisible()
  33 |       await page.screenshot({ path: `test-results/screens/dashboard-${user.role.toLowerCase()}.png`, fullPage: true })
  34 |     })
  35 |   }
  36 | 
  37 |   test('signing out returns to the login page and protects the dashboard again', async ({ page }) => {
  38 |     await page.goto('/login')
  39 |     await page.getByRole('button', { name: /^Teacher/ }).click()
  40 |     await expect(page).toHaveURL(/\/dashboard$/)
  41 | 
  42 |     await page.getByRole('button', { name: 'Sign out' }).click()
  43 | 
  44 |     await page.goto('/dashboard')
  45 |     await expect(page).toHaveURL(/\/login$/)
  46 |     await expect(page.getByRole('heading', { name: 'Sign in to SchoMans' })).toBeVisible()
  47 |   })
  48 | })
  49 | 
  50 | test.describe('guests and protected pages', () => {
  51 |   test('an anonymous visitor who opens the dashboard is sent to the login page', async ({ page }) => {
  52 |     await page.goto('/dashboard')
  53 | 
> 54 |     await expect(page).toHaveURL(/\/login$/)
     |                        ^ Error: expect(page).toHaveURL(expected) failed
  55 |   })
  56 | 
  57 |   test('a guest can browse the public page without an account', async ({ page }) => {
  58 |     await page.goto('/login')
  59 | 
  60 |     await page.getByRole('link', { name: 'Browse as guest' }).click()
  61 | 
  62 |     await expect(page).toHaveURL(/localhost:5173\/$/)
  63 |     await expect(page.getByRole('heading', { level: 1 })).toHaveText('School management in one place')
  64 |   })
  65 | 
  66 |   test('an unknown address shows the not-found page', async ({ page }) => {
  67 |     await page.goto('/this/does/not/exist')
  68 | 
  69 |     await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible()
  70 |   })
  71 | 
  72 |   test('a demo user opening the login page again is taken to the dashboard', async ({ page }) => {
  73 |     await page.goto('/login')
  74 |     await page.getByRole('button', { name: /^Student/ }).click()
  75 |     await expect(page).toHaveURL(/\/dashboard$/)
  76 | 
  77 |     // Navigating inside the app keeps the in-memory session (a full reload would not: tokens are never stored).
  78 |     await page.getByRole('link', { name: 'Dashboard' }).first().click()
  79 |     await expect(page.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')
  80 |   })
  81 | })
  82 | 
```