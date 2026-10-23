import { expect, test, type APIRequestContext } from '@playwright/test'

const keycloak = process.env.KEYCLOAK_URL ?? 'http://localhost:8080'
const realm = 'schomans'

// These tests use the real Keycloak login page (Authorization Code + PKCE), not the demo buttons.
test.describe('Keycloak login (OIDC + PKCE)', () => {
  test('a student signs in with email and password, survives a reload, and signs out', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in with your account' }).click()

    // The Keycloak page, themed as SchoMans.
    await expect(page).toHaveURL(new RegExp(`${keycloak}/realms/${realm}/`))
    await expect(page.getByText('SchoMans').first()).toBeVisible()
    await page.screenshot({ path: 'test-results/screens/keycloak-login-page.png' })
    await page.locator('#username').fill('student@schomans.demo')
    await page.locator('#password').fill('Demo123!')
    await page.locator('#kc-login').click()

    await expect(page).toHaveURL(/localhost:5173\/dashboard$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')
    await expect(page.getByText('Welcome, Rina Putri')).toBeVisible()

    // Tokens live in memory only, so a reload must resume the session from the Keycloak cookie (silent sign-in).
    await page.reload()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')

    await page.getByRole('button', { name: 'Sign out' }).click()
    await expect(page).toHaveURL(/localhost:5173\/login$/)

    // The Keycloak session is gone too: opening the dashboard sends the visitor to the login page again.
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login$/)
  })

  test('a wrong password is rejected by Keycloak', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in with your account' }).click()

    await page.locator('#username').fill('teacher@schomans.demo')
    await page.locator('#password').fill('not-the-password')
    await page.locator('#kc-login').click()

    await expect(page.getByText(/Invalid username or password/i)).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`${keycloak}/realms/${realm}/`))
  })

  test('opening a protected page first brings the user back to it after login', async ({ page }) => {
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login$/)

    await page.getByRole('button', { name: 'Sign in with your account' }).click()
    await page.locator('#username').fill('admin@schomans.demo')
    await page.locator('#password').fill('Demo123!')
    await page.locator('#kc-login').click()

    await expect(page).toHaveURL(/localhost:5173\/dashboard$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Admin dashboard')
  })
})

test.describe('Self-registration creates an Applicant', () => {
  const email = `e2e-${Date.now()}@schomans.demo`
  let adminToken = ''

  async function adminApi(request: APIRequestContext) {
    const response = await request.post(`${keycloak}/realms/master/protocol/openid-connect/token`, {
      form: {
        grant_type: 'password',
        client_id: 'admin-cli',
        username: process.env.KEYCLOAK_ADMIN ?? 'admin',
        password: process.env.KEYCLOAK_ADMIN_PASSWORD ?? 'admin',
      },
    })
    adminToken = ((await response.json()) as { access_token: string }).access_token
  }

  test.afterAll(async ({ request }) => {
    // Remove the account this test registered, so repeated runs leave Keycloak clean.
    await adminApi(request)
    const users = await request.get(`${keycloak}/admin/realms/${realm}/users`, {
      params: { email, exact: 'true' },
      headers: { Authorization: `Bearer ${adminToken}` },
    })
    for (const user of (await users.json()) as { id: string }[]) {
      await request.delete(`${keycloak}/admin/realms/${realm}/users/${user.id}`, { headers: { Authorization: `Bearer ${adminToken}` } })
    }
  })

  test('a guest registers on the Keycloak page and lands on the Applicant dashboard', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Sign in with your account' }).click()
    await page.getByRole('link', { name: 'Register' }).click()

    await page.locator('#firstName').fill('Gita')
    await page.locator('#lastName').fill('Pratama')
    await page.locator('#email').fill(email)
    await page.locator('#password').fill('Reg1ster!Me')
    await page.locator('#password-confirm').fill('Reg1ster!Me')
    await page.screenshot({ path: 'test-results/screens/keycloak-register-page.png' })
    await page.getByRole('button', { name: 'Register' }).click()

    await expect(page).toHaveURL(/localhost:5173\/dashboard$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Applicant dashboard')
    await expect(page.getByText('Welcome, Gita Pratama')).toBeVisible()
  })
})
