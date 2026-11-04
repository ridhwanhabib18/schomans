import { expect, test } from '@playwright/test'
import { goInApp, loginAs, tokenFor, unique } from './support/helpers.ts'

const api = process.env.E2E_API_URL ?? 'http://localhost:5080'

// Phase 6: the principal asks the assistant (mock provider, real tools), the audit log shows a change with who made it,
// and the app can be installed as a PWA.
test('the principal asks the assistant and gets an answer with its sources', async ({ page }) => {
  await loginAs(page, 'Principal')
  await page.getByRole('link', { name: 'Assistant' }).first().click()
  await expect(page.getByRole('heading', { name: 'Assistant' })).toBeVisible()

  await page.getByLabel('Your question').fill('Which requests are waiting for approval?')
  await page.getByRole('button', { name: 'Ask' }).click()

  await expect(page.getByText('Where the numbers come from')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Open the report' }).first()).toBeVisible()
  await page.screenshot({ path: 'test-results/phase-6-assistant.png', fullPage: true })
})

test('a new request shows up in the audit log with the person who made it', async ({ page, request }) => {
  const reason = `E2E audit ${unique()}`
  const parent = { Authorization: `Bearer ${await tokenFor(request, 'Parent')}` }
  const children = (await (await request.get(`${api}/api/v1/users/me/children`, { headers: parent })).json()) as { studentId: string }[]
  const day = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10)
  const created = await request.post(`${api}/api/v1/requests`, {
    headers: parent,
    data: { type: 'StudentLeave', studentId: children[0]?.studentId, startDate: day, endDate: day, reason },
  })
  expect(created.status()).toBe(201)
  const { id } = (await created.json()) as { id: string }

  try {
    await loginAs(page, 'Admin staff')
    await page.getByRole('link', { name: 'Audit log' }).first().click()
    await expect(page.getByRole('heading', { name: 'Audit log' })).toBeVisible()

    await page.getByLabel('Kind of record').selectOption('Request')
    const row = page.getByRole('row', { name: /Agus Putra/ }).filter({ hasText: 'Created' }).first()
    await expect(row).toBeVisible()
    await expect(row).toContainText('Status: Pending')
    await page.screenshot({ path: 'test-results/phase-6-audit-log.png', fullPage: true })
  } finally {
    await request.post(`${api}/api/v1/requests/${id}/cancel`, { headers: parent })
  }
})

test('only admin staff and the principal see the audit log', async ({ page }) => {
  await loginAs(page, 'Teacher')

  await expect(page.getByRole('link', { name: 'Audit log' })).toHaveCount(0)
  await goInApp(page, '/manage/audit-log')
  await expect(page.getByRole('heading', { name: 'Audit log' })).toHaveCount(0)
})

test('the app can be installed: a manifest with icons is served', async ({ request }) => {
  const manifest = (await (await request.get('/manifest.webmanifest')).json()) as { name: string; display: string; icons: { src: string; sizes: string }[] }

  expect(manifest.name).toBe('SchoMans')
  expect(manifest.display).toBe('standalone')
  expect(manifest.icons.map((icon) => icon.sizes)).toEqual(expect.arrayContaining(['192x192', '512x512']))
  for (const icon of manifest.icons) {
    const response = await request.get(icon.src)
    expect(response.ok(), icon.src).toBe(true)
    expect(response.headers()['content-type']).toContain('image/png')
  }
  expect((await request.get('/sw.js')).ok()).toBe(true)
})
