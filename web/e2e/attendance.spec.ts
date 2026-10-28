import { expect, test } from '@playwright/test'
import { createLessonToday, goInApp, loginAs } from './support/helpers.ts'

// Phase 3 exit criterion: the attendance flow works end to end. The teacher opens a lesson, the QR code rotates every
// minute (with a 10 second grace), the student checks in with the code, the teacher sees it live, closes the lesson and
// reports on the month. A camera cannot be pointed at a screen here, so the check-in address inside the code is opened
// the way the phone's camera app would open it.
test('a teacher takes attendance with the rotating QR code and a student checks in', async ({ browser, request }) => {
  test.setTimeout(240_000)
  const { time } = await createLessonToday(request)
  const teacherContext = await browser.newContext()
  const studentContext = await browser.newContext()
  const parentContext = await browser.newContext()

  try {
    const teacher = await teacherContext.newPage()
    const student = await studentContext.newPage()
    await loginAs(teacher, 'Teacher')
    await loginAs(student, 'Student')

    // The teacher opens the lesson from the list of today's lessons.
    await teacher.getByRole('link', { name: 'Attendance', exact: true }).first().click()
    const lesson = teacher.getByRole('listitem').filter({ hasText: time })
    await lesson.getByRole('button', { name: /Open attendance/ }).click()
    await expect(teacher).toHaveURL(/\/attendance\/sessions\/[0-9a-f-]{36}$/)
    await expect(teacher.getByText('Live', { exact: true })).toBeVisible()
    await expect(teacher.getByText('Mathematics, Class A')).toBeVisible()
    await expect(teacher.getByText(/^0 present, \d+ not scanned$/)).toBeVisible()

    const qr = teacher.getByAltText('QR code to check in to this lesson')
    await expect(qr).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/attendance-session-qr.png', fullPage: true })
    const firstUrl = (await qr.getAttribute('data-checkin-url'))!
    expect(firstUrl).toContain('/attend?t=')

    // The code rotates by itself every minute and the new one reaches the teacher's screen live.
    await expect.poll(async () => qr.getAttribute('data-checkin-url'), { timeout: 100_000, intervals: [2_000] }).not.toBe(firstUrl)
    const secondUrl = (await qr.getAttribute('data-checkin-url'))!

    // Past the 10 second grace the old code is refused: a photo of the screen is useless.
    await teacher.waitForTimeout(11_000)
    await goInApp(student, new URL(firstUrl).pathname + new URL(firstUrl).search)
    await expect(student.getByText('You could not be checked in')).toBeVisible()
    await expect(student.getByText('This code has expired. Scan the code that is on the screen now.')).toBeVisible()
    await student.screenshot({ path: 'test-results/screens/attendance-checkin-expired.png' })

    // The current code works, and the teacher sees the student arrive without a reload.
    await goInApp(student, new URL(secondUrl).pathname + new URL(secondUrl).search)
    await expect(student.getByText('You are checked in')).toBeVisible()
    await expect(student.getByText('Mathematics, Class A: Present')).toBeVisible()
    await student.screenshot({ path: 'test-results/screens/attendance-checkin-success.png' })
    await expect(teacher.getByText(/^1 present, \d+ not scanned$/)).toBeVisible()
    const rina = teacher.getByRole('listitem').filter({ hasText: 'Rina Putri' })
    await expect(rina.getByText('Present', { exact: true })).toBeVisible()

    // Scanning again changes nothing.
    await goInApp(student, '/attendance/me') // a second scan is a new visit of the address, not the same page again
    await goInApp(student, new URL(secondUrl).pathname + new URL(secondUrl).search)
    await expect(student.getByText('You were already checked in')).toBeVisible()

    // A phone without a camera: the teacher marks a student sick by hand, with a note.
    const other = teacher.getByRole('listitem').filter({ hasText: 'Not scanned yet' }).first()
    // Names repeat in the demo school; the NISN is unique.
    const nisn = ((await other.locator('p.text-xs').textContent()) ?? '').trim().split(' ')[0]!
    await other.getByRole('button', { name: /^Change/ }).click()
    const dialog = teacher.getByRole('dialog')
    await dialog.getByLabel('Sick').check()
    await dialog.getByLabel(/Note/).fill('Letter from home')
    await dialog.getByRole('button', { name: 'Save' }).click()
    await expect(teacher.getByRole('listitem').filter({ hasText: nisn }).getByText('Sick', { exact: true })).toBeVisible()
    await expect(teacher.getByText('Letter from home')).toBeVisible()

    // Closing marks everybody who did not scan absent.
    await teacher.getByRole('button', { name: 'Close attendance' }).click()
    await teacher.getByRole('alertdialog').getByRole('button', { name: 'Close attendance' }).click()
    await expect(teacher.getByText(/Attendance is closed/)).toBeVisible()
    await expect(teacher.getByAltText('QR code to check in to this lesson')).toHaveCount(0)
    await expect(teacher.getByText('Absent', { exact: true }).first()).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/attendance-session-closed.png', fullPage: true })

    // Nobody can check in after closing.
    await goInApp(student, '/attendance/me') // a second scan is a new visit of the address, not the same page again
    await goInApp(student, new URL(secondUrl).pathname + new URL(secondUrl).search)
    await expect(student.getByText('You were already checked in')).toBeVisible() // Rina's record stays Present

    // The student sees the lesson in their own month.
    await student.getByRole('link', { name: 'My attendance' }).first().click()
    await expect(student.getByRole('heading', { level: 1, name: 'My attendance' })).toBeVisible()
    await expect(student.getByRole('cell', { name: 'Mathematics' }).first()).toBeVisible()
    await student.screenshot({ path: 'test-results/screens/attendance-student-month.png', fullPage: true })

    // A parent sees the same for their child.
    const parent = await parentContext.newPage()
    await loginAs(parent, 'Parent')
    await parent.getByRole('link', { name: 'Attendance', exact: true }).first().click()
    await expect(parent.getByRole('heading', { level: 1, name: 'Attendance' })).toBeVisible()
    await parent.getByLabel('Child').selectOption({ label: 'Rina Putri (Class A)' })
    await expect(parent.getByRole('cell', { name: 'Mathematics' }).first()).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/attendance-parent-view.png', fullPage: true })

    // The month report shows the counts and exports to Excel.
    await teacher.getByRole('link', { name: 'Attendance reports' }).first().click()
    await expect(teacher.getByRole('heading', { level: 1, name: 'Attendance reports' })).toBeVisible()
    await expect(teacher.getByText(/lesson\(s\) with attendance this month/)).toBeVisible()
    await expect(teacher.getByRole('cell', { name: 'Rina Putri', exact: true })).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/attendance-report.png', fullPage: true })
    const download = teacher.waitForEvent('download')
    await teacher.getByRole('button', { name: 'Export to Excel' }).click()
    const file = await download
    expect(file.suggestedFilename()).toMatch(/^attendance-Class-A-\d{4}-\d{2}\.xlsx$/)
    const path = await file.path()
    const { readFile } = await import('node:fs/promises')
    expect((await readFile(path)).subarray(0, 2).toString()).toBe('PK') // an .xlsx file is a zip
  } finally {
    await Promise.all([teacherContext.close(), studentContext.close(), parentContext.close()])
  }
})

test('a student reaches the check-in page with a camera app after signing in with Keycloak', async ({ page }) => {
  // The address a camera app opens: a full page load. Tokens live in memory, so the sign-in comes first and the
  // address is kept through it (the code is in the query string).
  await page.goto('/attend?t=0123456789abcdef0123456789abcdef.AbC123')
  await expect(page).toHaveURL(/\/login$/)

  await page.getByRole('button', { name: 'Sign in with your account' }).click()
  await page.locator('#username').fill('student@schomans.demo')
  await page.locator('#password').fill('Demo123!')
  await page.locator('#kc-login').click()

  await expect(page).toHaveURL(/\/attend\?t=0123456789abcdef0123456789abcdef\.AbC123$/)
  // The code is not a real session, so the API says it is not a SchoMans code; the point is that the code survived the sign-in.
  await expect(page.getByText('This is not a SchoMans attendance code.')).toBeVisible()
})

test('roles that do not take attendance see no attendance pages of the teacher', async ({ page }) => {
  await loginAs(page, 'Student')

  await expect(page.getByRole('link', { name: 'Attendance reports' })).toHaveCount(0)
  await goInApp(page, '/attendance')
  await expect(page.getByRole('alert')).toContainText('You do not have access to this page')
})
