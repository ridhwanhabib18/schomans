import { expect, test, type Browser, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import path from 'node:path'
import { adminApi, createLessonToday, goInApp, loginAs, unique } from '../e2e/support/helpers.ts'

// The main flows, step by step: each step stores a picture in test-results/capture-frames/<flow>/, and capture/make-gifs.py turns
// every folder into docs/assets/flows/<flow>.gif. The steps follow the end-to-end tests of the phases; only fictional demo data is shown.
const framesRoot = path.resolve(process.cwd(), 'test-results', 'capture-frames')

async function frame(page: Page, flow: string, step: number, label: string): Promise<void> {
  const dir = path.join(framesRoot, flow)
  mkdirSync(dir, { recursive: true })
  await page.waitForTimeout(500)
  await page.screenshot({ path: path.join(dir, `${String(step).padStart(2, '0')}-${label}.png`) })
}

const phone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }
const dayFromNow = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10)

async function openAs(browser: Browser, role: Parameters<typeof loginAs>[1], options: Parameters<Browser['newContext']>[0] = {}) {
  const context = await browser.newContext(options)
  const page = await context.newPage()
  await loginAs(page, role)
  return { context, page }
}

test('flow: QR attendance', async ({ browser, request }) => {
  const { time } = await createLessonToday(request)
  const teacher = await openAs(browser, 'Teacher')
  const student = await openAs(browser, 'Student', phone)
  try {
    await teacher.page.getByRole('link', { name: 'Attendance', exact: true }).first().click()
    await expect(teacher.page.getByRole('listitem').filter({ hasText: time })).toBeVisible()
    await frame(teacher.page, 'qr-attendance', 1, 'todays-lessons')

    await teacher.page.getByRole('listitem').filter({ hasText: time }).getByRole('button', { name: /Open attendance/ }).click()
    const qr = teacher.page.getByAltText('QR code to check in to this lesson')
    await expect(qr).toBeVisible()
    await frame(teacher.page, 'qr-attendance', 2, 'qr-on-the-projector')

    // The phone's camera opens the address inside the code; here the student's page opens it the same way.
    const url = new URL((await qr.getAttribute('data-checkin-url'))!)
    await goInApp(student.page, url.pathname + url.search)
    await expect(student.page.getByText('You are checked in')).toBeVisible()
    await frame(student.page, 'qr-attendance', 3, 'student-checked-in')

    await expect(teacher.page.getByText(/^1 present, \d+ not scanned$/)).toBeVisible()
    await frame(teacher.page, 'qr-attendance', 4, 'teacher-sees-it-live')
  } finally {
    await Promise.all([teacher.context.close(), student.context.close()])
  }
})

test('flow: assignments', async ({ browser, request }) => {
  const title = `Fractions homework ${unique()}`
  const teacher = await openAs(browser, 'Teacher')
  const student = await openAs(browser, 'Student')
  const parent = await openAs(browser, 'Parent')
  let id: string | undefined
  try {
    await teacher.page.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.page.getByRole('link', { name: 'New assignment' }).click()
    await teacher.page.getByLabel(/^Title/).fill(title)
    await teacher.page.getByLabel(/^Subject/).selectOption({ label: 'Mathematics' })
    await teacher.page.getByLabel('Class A').check()
    await teacher.page.getByLabel(/Instructions/).fill('Solve the exercises on page 12.')
    await frame(teacher.page, 'assignments', 1, 'teacher-writes-it')
    await teacher.page.getByRole('button', { name: 'Give assignment' }).click()
    await expect(teacher.page.getByRole('heading', { level: 1, name: title })).toBeVisible()
    id = /\/assignments\/([0-9a-f-]{36})$/.exec(teacher.page.url())?.[1]

    await expect(student.page.getByRole('status').getByText(`New assignment: ${title}`)).toBeVisible()
    await frame(student.page, 'assignments', 2, 'student-is-told')

    await student.page.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await student.page.getByRole('link', { name: new RegExp(title) }).click()
    await student.page.getByLabel('Or type your answer').fill('The answer is three quarters.')
    await frame(student.page, 'assignments', 3, 'student-answers')
    await student.page.getByRole('button', { name: 'Hand in' }).click()
    await expect(student.page.getByText('Handed in', { exact: false }).first()).toBeVisible()

    await teacher.page.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.page.getByRole('link', { name: title }).click()
    await teacher.page.getByRole('row', { name: /Rina Putri/ }).getByRole('button', { name: /^Review/ }).click()
    const dialog = teacher.page.getByRole('dialog', { name: 'Work of Rina Putri' })
    await dialog.getByLabel(/Score/).fill('88.5')
    await dialog.getByLabel('Feedback', { exact: true }).fill('Well explained.')
    await frame(teacher.page, 'assignments', 4, 'teacher-scores-it')
    await dialog.getByRole('button', { name: 'Save score' }).click()

    await expect(student.page.getByRole('status').getByText(`Your work was scored: ${title}`)).toBeVisible()
    await goInApp(student.page, '/assignments')
    await student.page.getByRole('link', { name: new RegExp(title) }).click()
    await expect(student.page.getByText('Well explained.')).toBeVisible()
    await frame(student.page, 'assignments', 5, 'student-sees-the-score')

    await parent.page.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await parent.page.getByLabel('Child').selectOption({ label: 'Rina Putri (Class A)' })
    await expect(parent.page.getByRole('link', { name: new RegExp(title) })).toContainText('Score 88.5')
    await frame(parent.page, 'assignments', 6, 'parent-follows-it')
  } finally {
    if (id) {
      await (await adminApi(request)).del(`/api/v1/assignments/${id}`)
    }
    await Promise.all([teacher.context.close(), student.context.close(), parent.context.close()])
  }
})

test('flow: approvals', async ({ browser }) => {
  const reason = `Fever, a doctor's note follows ${unique()}`
  const parent = await openAs(browser, 'Parent')
  const teacher = await openAs(browser, 'Teacher')
  const principal = await openAs(browser, 'Principal')
  const link = (page: Page, id: string) => page.locator(`a[href="/requests/${id}"]`)
  try {
    await parent.page.getByRole('link', { name: 'Requests', exact: true }).first().click()
    await parent.page.getByRole('link', { name: 'New request' }).click()
    await parent.page.getByLabel('What do you ask for?').selectOption({ label: 'Student leave' })
    await parent.page.getByLabel('First day').fill(dayFromNow(1))
    await parent.page.getByLabel('Last day').fill(dayFromNow(2))
    await parent.page.getByLabel('Reason').fill(reason)
    await frame(parent.page, 'approvals', 1, 'parent-asks-for-leave')
    await parent.page.getByRole('button', { name: 'Send the request' }).click()
    await expect(parent.page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/)
    const id = parent.page.url().split('/').pop() as string
    await frame(parent.page, 'approvals', 2, 'request-waits-for-the-teacher')

    await teacher.page.getByRole('link', { name: 'Approvals', exact: true }).first().click()
    await link(teacher.page, id).click()
    await expect(teacher.page.getByText('This request waits for your decision.')).toBeVisible()
    await frame(teacher.page, 'approvals', 3, 'homeroom-teacher-decides')
    await teacher.page.getByRole('button', { name: 'Approve', exact: true }).click()
    await teacher.page.getByRole('alertdialog').getByLabel('Note').fill('Get well soon')
    await teacher.page.getByRole('alertdialog').getByRole('button', { name: 'Approve', exact: true }).click()
    await expect(teacher.page.getByText('Your decision is saved').first()).toBeVisible()

    await principal.page.getByRole('link', { name: 'Approvals', exact: true }).first().click()
    await link(principal.page, id).click()
    await expect(principal.page.getByText('This request waits for your decision.')).toBeVisible()
    await frame(principal.page, 'approvals', 4, 'principal-decides')
    await principal.page.getByRole('button', { name: 'Approve', exact: true }).click()
    await principal.page.getByRole('alertdialog').getByRole('button', { name: 'Approve', exact: true }).click()
    await expect(principal.page.getByText('Your decision is saved').first()).toBeVisible()

    await parent.page.getByRole('link', { name: 'Requests', exact: true }).first().click()
    await expect(parent.page.getByRole('row').filter({ has: link(parent.page, id) }).getByText('Approved')).toBeVisible()
    await frame(parent.page, 'approvals', 5, 'parent-sees-it-approved')
  } finally {
    await Promise.all([parent.context.close(), teacher.context.close(), principal.context.close()])
  }
})

test('flow: admissions', async ({ browser }) => {
  const person = await browser.newContext()
  const page = await person.newPage()
  const staff = await openAs(browser, 'Admin staff')
  try {
    await page.goto('/login')
    await page.getByRole('button', { name: /^Applicant/ }).click()
    await page.waitForURL((url) => !url.pathname.endsWith('/login'))
    await goInApp(page, '/admissions')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await frame(page, 'admissions', 1, 'applicant-follows-the-application')

    await staff.page.getByRole('link', { name: 'Admissions', exact: true }).first().click()
    await expect(staff.page.getByRole('heading', { level: 1 })).toBeVisible()
    await frame(staff.page, 'admissions', 2, 'staff-see-the-applications')

    const first = staff.page.locator('main a[href^="/manage/admissions/"]').first()
    if (await first.count()) {
      await first.click()
      await expect(staff.page.getByRole('heading', { level: 1 })).toBeVisible()
      await frame(staff.page, 'admissions', 3, 'staff-verify-the-documents')
    }

    await staff.page.getByRole('link', { name: 'Admission periods', exact: true }).first().click()
    await expect(staff.page.getByRole('heading', { level: 1 })).toBeVisible()
    await frame(staff.page, 'admissions', 4, 'admission-periods')
  } finally {
    await Promise.all([person.close(), staff.context.close()])
  }
})

test('flow: AI features', async ({ browser }) => {
  const principal = await openAs(browser, 'Principal')
  const staff = await openAs(browser, 'Admin staff')
  const teacher = await openAs(browser, 'Teacher')
  const guest = await browser.newContext()
  try {
    await principal.page.getByRole('link', { name: 'Assistant' }).first().click()
    await principal.page.getByLabel('Your question').fill('Which requests are waiting for approval?')
    await principal.page.getByRole('button', { name: 'Ask' }).click()
    await expect(principal.page.getByText('Where the numbers come from')).toBeVisible()
    await frame(principal.page, 'ai-features', 1, 'principal-assistant')

    await staff.page.getByRole('link', { name: 'Manage announcements' }).first().click()
    await staff.page.getByRole('link', { name: 'New announcement' }).click()
    await staff.page.getByRole('button', { name: 'Draft with AI' }).click()
    const dialog = staff.page.getByRole('dialog', { name: 'Draft the announcement with AI' })
    await dialog.getByLabel(/^Who is it for/).fill('Parents of class 7A')
    await dialog.getByLabel(/^Short notes/).fill('The school trip is on Friday at 7 AM. Bring a packed lunch.')
    await frame(staff.page, 'ai-features', 2, 'draft-dialog')
    await dialog.getByRole('button', { name: 'Write a draft' }).click()
    await expect(dialog).toHaveCount(0)
    await frame(staff.page, 'ai-features', 3, 'draft-in-the-editor')

    await teacher.page.getByRole('link', { name: 'Question bank' }).first().click()
    await expect(teacher.page.getByRole('heading', { level: 1 })).toBeVisible()
    await frame(teacher.page, 'ai-features', 4, 'question-bank')

    const visitor = await guest.newPage()
    await visitor.goto('/')
    const widget = visitor.getByRole('region', { name: 'Ask the school' })
    await widget.getByLabel('Your question').fill('When does the admission open?')
    await widget.getByRole('button', { name: 'Ask' }).click()
    await expect(widget.getByText(/Source:|I could not find/)).toBeVisible()
    await frame(visitor, 'ai-features', 5, 'school-chatbot')
  } finally {
    await Promise.all([principal.context.close(), staff.context.close(), teacher.context.close(), guest.close()])
  }
})
