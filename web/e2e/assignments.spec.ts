import { expect, test } from '@playwright/test'
import { goInApp, loginAs, tinyPng, unique } from './support/helpers.ts'

// Phase 3 exit criterion: the assignment flow works end to end, through the real storage (SeaweedFS): the teacher gives
// work with an attachment, the student is notified at once, opens the attachment, hands in a file, the teacher scores
// it, and the student and the parent see the score.
test('a teacher gives an assignment, a student hands in work, and the score reaches student and parent', async ({ browser }) => {
  test.setTimeout(180_000)
  const title = `E2E Fractions ${unique()}`
  const teacherContext = await browser.newContext()
  const studentContext = await browser.newContext()
  const parentContext = await browser.newContext()

  try {
    const teacher = await teacherContext.newPage()
    const student = await studentContext.newPage()
    const parent = await parentContext.newPage()
    await loginAs(student, 'Student')
    await loginAs(teacher, 'Teacher')

    // The teacher gives an assignment to Class A, with a file attached (uploaded straight to storage).
    await teacher.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.getByRole('link', { name: 'New assignment' }).click()
    await expect(teacher.getByRole('heading', { level: 1, name: 'New assignment' })).toBeVisible()
    await teacher.getByLabel(/^Title/).fill(title)
    await teacher.getByLabel(/^Subject/).selectOption({ label: 'Mathematics' })
    await teacher.getByLabel('Class A').check()
    await teacher.getByLabel(/Instructions/).fill('Solve the exercises on page 12.')
    await teacher.getByLabel('Add a file', { exact: true }).setInputFiles({ name: 'page-12.png', mimeType: 'image/png', buffer: tinyPng })
    await expect(teacher.getByText('page-12.png')).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/assignment-form.png', fullPage: true })
    await teacher.getByRole('button', { name: 'Give assignment' }).click()
    await expect(teacher.getByRole('heading', { level: 1, name: title })).toBeVisible()

    // The student's open page shows a toast and the bell fills, without a reload.
    await expect(student.getByRole('status').getByText(`New assignment: ${title}`)).toBeVisible()

    // The student opens it from the list, sees the instructions, and opens the teacher's attachment (a short-lived link).
    await student.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await student.getByRole('link', { name: new RegExp(title) }).click()
    await expect(student.getByRole('heading', { level: 1, name: title })).toBeVisible()
    await expect(student.getByText('Solve the exercises on page 12.')).toBeVisible()
    await expect(student.getByText('Not handed in yet.')).toBeVisible()
    const popup = student.waitForEvent('popup')
    await student.getByRole('button', { name: /page-12\.png/ }).click()
    const opened = await popup
    expect(opened.url()).toMatch(/\/schomans-private\/assignments\/uploads\/.+\.png\?/)
    expect(opened.url()).toContain('X-Amz-Signature=')
    await opened.close()
    await student.screenshot({ path: 'test-results/screens/assignment-student-open.png', fullPage: true })

    // The student hands in a file and a note. A file of the wrong type is refused before it is uploaded.
    await student.getByLabel('File (PDF or image, at most 10 MB)', { exact: true }).setInputFiles({ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('hi') })
    await expect(student.getByText('Hand in a PDF or an image (JPEG, PNG, WebP).')).toBeVisible()
    await student.getByLabel('File (PDF or image, at most 10 MB)', { exact: true }).setInputFiles({ name: 'answer.png', mimeType: 'image/png', buffer: tinyPng })
    await student.getByLabel('Or type your answer').fill('The answer is three quarters.')
    await student.getByRole('button', { name: 'Hand in' }).click()
    await expect(student.getByText('Handed in', { exact: false }).first()).toBeVisible()
    await expect(student.getByRole('button', { name: /answer\.png/ })).toBeVisible()
    await expect(student.getByRole('button', { name: 'Hand in again' })).toBeVisible()
    await student.screenshot({ path: 'test-results/screens/assignment-student-submitted.png', fullPage: true })

    // The teacher sees the work in the review list, opens it and scores it with feedback.
    await teacher.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.getByRole('link', { name: title }).click()
    await expect(teacher.getByText(/1 of \d+ handed in, 0 scored/)).toBeVisible()
    const row = teacher.getByRole('row', { name: /Rina Putri/ })
    await expect(row.getByText('Submitted')).toBeVisible()
    await row.getByRole('button', { name: /^Review/ }).click()
    const dialog = teacher.getByRole('dialog', { name: 'Work of Rina Putri' })
    await expect(dialog.getByText('The answer is three quarters.')).toBeVisible()
    await dialog.getByLabel(/Score/).fill('88.5')
    await dialog.getByLabel('Feedback').fill('Well explained.')
    await teacher.screenshot({ path: 'test-results/screens/assignment-review.png' })
    await dialog.getByRole('button', { name: 'Save score' }).click()
    await expect(teacher.getByText(/1 of \d+ handed in, 1 scored/)).toBeVisible()

    // The student is notified, sees the score and the feedback, and can no longer change the work.
    await expect(student.getByRole('status').getByText(`Your work was scored: ${title}`)).toBeVisible()
    await goInApp(student, '/assignments')
    await student.getByRole('link', { name: new RegExp(title) }).click()
    await expect(student.locator('dd', { hasText: /^88\.5$/ })).toBeVisible()
    await expect(student.getByText('Well explained.')).toBeVisible()
    await expect(student.getByText('Your work is scored, so it can no longer be changed.')).toBeVisible()
    await expect(student.getByRole('button', { name: /Hand in/ })).toHaveCount(0)
    await student.screenshot({ path: 'test-results/screens/assignment-student-scored.png', fullPage: true })

    // The parent follows the same on the pages of their child.
    await loginAs(parent, 'Parent')
    await parent.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await expect(parent.getByRole('heading', { level: 1, name: 'Assignments' })).toBeVisible()
    await parent.getByLabel('Child').selectOption({ label: 'Rina Putri (Class A)' })
    const card = parent.getByRole('link', { name: new RegExp(title) })
    await expect(card).toContainText('Score 88.5')
    await card.click()
    await expect(parent.getByText('Work of your child')).toBeVisible()
    await expect(parent.locator('dd', { hasText: /^88\.5$/ })).toBeVisible()
    await expect(parent.getByRole('button', { name: /Hand in/ })).toHaveCount(0)
    await parent.screenshot({ path: 'test-results/screens/assignment-parent-view.png', fullPage: true })
  } finally {
    await Promise.all([teacherContext.close(), studentContext.close(), parentContext.close()])
  }
})

test('a teacher can delete an assignment nobody has handed in', async ({ page }) => {
  const title = `E2E Spare ${unique()}`
  await loginAs(page, 'Teacher')
  await page.getByRole('link', { name: 'Assignments', exact: true }).first().click()
  await page.getByRole('link', { name: 'New assignment' }).click()
  await page.getByLabel(/^Title/).fill(title)
  await page.getByLabel(/^Subject/).selectOption({ label: 'Mathematics' })
  await page.getByLabel('Class B').check()
  await page.getByRole('button', { name: 'Give assignment' }).click()
  await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()

  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Delete' }).click()

  await expect(page.getByRole('heading', { level: 1, name: 'Assignments' })).toBeVisible()
  await expect(page.getByRole('link', { name: title })).toHaveCount(0)
})

test('a student cannot give assignments and sees only their own', async ({ page }) => {
  await loginAs(page, 'Student')

  await expect(page.getByRole('link', { name: 'New assignment' })).toHaveCount(0)
  await goInApp(page, '/assignments/new')
  await expect(page.getByRole('alert')).toContainText('You do not have access to this page')
})
