import { expect, test, type Page } from '@playwright/test'
import { adminApi, loginAs, unique, unreadCount } from './support/helpers.ts'

async function writeAnnouncement(page: Page, title: string, options: { role?: string; isPublic?: boolean } = {}) {
  await page.getByRole('link', { name: 'Manage announcements' }).first().click()
  await page.getByRole('link', { name: 'New announcement' }).click()
  await page.getByLabel(/^Title/).fill(title)
  await page.getByRole('textbox', { name: 'Announcement message' }).click()
  await page.keyboard.type('Please read this message carefully.')
  if (options.role) {
    await page.getByRole('checkbox', { name: options.role, exact: true }).check()
  }
  if (options.isPublic) {
    await page.getByRole('checkbox', { name: 'Also show on the public pages' }).check()
  }
  await page.getByRole('button', { name: 'Save draft' }).click()
  await expect(page).toHaveURL(/\/manage\/announcements\/[0-9a-f-]{36}$/)
  return /\/manage\/announcements\/([0-9a-f-]{36})$/.exec(page.url())![1]!
}

async function publish(page: Page) {
  await page.getByRole('button', { name: 'Publish' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Publish' }).click()
  await expect(page).toHaveURL(/\/manage\/announcements$/)
}

// Phase 2 exit criterion: announcements appear in real time.
test('a published announcement reaches its audience at once, and only its audience', async ({ browser, request }) => {
  const title = `E2E schedule change ${unique()}`
  let id: string | undefined
  const admin = await browser.newContext()
  const teacher = await browser.newContext()
  const student = await browser.newContext()

  try {
    const adminPage = await admin.newPage()
    const teacherPage = await teacher.newPage()
    const studentPage = await student.newPage()
    await loginAs(teacherPage, 'Teacher')
    await loginAs(studentPage, 'Student')
    await loginAs(adminPage, 'Admin staff')
    const teacherBefore = await unreadCount(teacherPage)
    const studentBefore = await unreadCount(studentPage)

    id = await writeAnnouncement(adminPage, title, { role: 'Teacher' })
    await adminPage.screenshot({ path: 'test-results/screens/announcement-editor.png', fullPage: true })
    await publish(adminPage)

    // No reload anywhere: the teacher's page gets a toast and a badge from the SignalR push.
    await expect(teacherPage.getByRole('status').getByText(title)).toBeVisible()
    await expect(teacherPage.getByRole('button', { name: `Notifications, ${teacherBefore + 1} unread` })).toBeVisible()
    await teacherPage.screenshot({ path: 'test-results/screens/announcement-realtime-toast.png' })

    // The student is not in the audience: nothing arrives.
    await expect(studentPage.getByRole('status').getByText(title)).toHaveCount(0)
    expect(await unreadCount(studentPage)).toBe(studentBefore)

    // Opening it from the bell marks it read and clears the badge.
    await teacherPage.getByRole('button', { name: /^Notifications/ }).click()
    await teacherPage.getByRole('region', { name: 'Notifications' }).getByRole('button', { name: new RegExp(title) }).click()
    await expect(teacherPage).toHaveURL(new RegExp(`/announcements/${id}$`))
    await expect(teacherPage.getByRole('heading', { level: 1, name: title })).toBeVisible()
    await expect.poll(() => unreadCount(teacherPage)).toBe(teacherBefore)

    // The student's inbox does not list it.
    await studentPage.getByRole('link', { name: 'Announcements', exact: true }).first().click()
    await expect(studentPage.getByRole('heading', { level: 1, name: 'Announcements' })).toBeVisible()
    await expect(studentPage.getByText(title)).toHaveCount(0)
  } finally {
    if (id) {
      await (await adminApi(request)).del(`/api/v1/announcements/${id}`)
    }
    await Promise.all([admin.close(), teacher.close(), student.close()])
  }
})

test('an announcement marked public is readable by a guest, a private one is not', async ({ browser, request }) => {
  const publicTitle = `E2E open day ${unique()}`
  const privateTitle = `E2E staff only ${unique()}`
  const ids: string[] = []
  const admin = await browser.newContext()
  const guest = await browser.newContext()

  try {
    const adminPage = await admin.newPage()
    await loginAs(adminPage, 'Admin staff')
    ids.push(await writeAnnouncement(adminPage, publicTitle, { isPublic: true }))
    await publish(adminPage)
    ids.push(await writeAnnouncement(adminPage, privateTitle))
    await publish(adminPage)

    const guestPage = await guest.newPage()
    await guestPage.goto('/')
    await expect(guestPage.getByRole('link', { name: new RegExp(publicTitle) })).toBeVisible()
    await expect(guestPage.getByText(privateTitle)).toHaveCount(0)
    await guestPage.screenshot({ path: 'test-results/screens/landing-with-announcements.png', fullPage: true })

    await guestPage.getByRole('link', { name: new RegExp(publicTitle) }).click()
    await expect(guestPage.getByRole('heading', { level: 1, name: publicTitle })).toBeVisible()
    await expect(guestPage.getByText('Please read this message carefully.')).toBeVisible()
  } finally {
    const api = await adminApi(request)
    for (const id of ids) {
      await api.del(`/api/v1/announcements/${id}`)
    }
    await Promise.all([admin.close(), guest.close()])
  }
})
