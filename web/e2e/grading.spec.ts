import { expect, test } from '@playwright/test'
import { loginAs } from './support/helpers.ts'

// Phase 4 exit criterion: report-card PDFs generate and the charts show per role. The teacher enters a score and a report
// text, publishes the grades of Class A and generates the report cards; the student and the parent then see the grades
// and open the PDF through the real storage (SeaweedFS). Needs the demo data (teacher of Class A and Mathematics, an
// active term) and a running stack, like the other specs. The grades are unpublished at the end so the test can run again.
test('a teacher publishes grades and report cards, and student and parent see them', async ({ browser }) => {
  test.setTimeout(180_000)
  const teacherContext = await browser.newContext()
  const studentContext = await browser.newContext()
  const parentContext = await browser.newContext()

  try {
    const teacher = await teacherContext.newPage()
    const student = await studentContext.newPage()
    const parent = await parentContext.newPage()
    await loginAs(teacher, 'Teacher')

    // The grade book: pick Class A and Mathematics (the current term is preselected) and type a midterm score.
    await teacher.getByRole('link', { name: 'Grade book', exact: true }).first().click()
    await expect(teacher.getByRole('heading', { level: 1, name: 'Grade book' })).toBeVisible()
    await teacher.getByLabel('Class and subject').selectOption({ label: 'Class A - Mathematics' })
    const row = teacher.getByRole('row', { name: /Rina Putri/ })
    await expect(row).toBeVisible()

    // A grade book that was left published by an earlier run is opened up first.
    if (await teacher.getByRole('button', { name: 'Unpublish grades' }).isVisible()) {
      await teacher.getByRole('button', { name: 'Unpublish grades' }).click()
      await teacher.getByRole('alertdialog').getByRole('button', { name: 'Unpublish the grades?' }).click()
      await expect(teacher.getByText('Grades unpublished')).toBeVisible()
    }

    // The demo data already holds a midterm score for every student; a typed score only saves when it differs from it.
    const midterm = teacher.getByLabel('Midterm score of Rina Putri')
    await midterm.fill((await midterm.inputValue()) === '92' ? '91' : '92')
    await midterm.blur()
    await expect(teacher.getByText('Score saved')).toBeVisible()

    await row.getByRole('button', { name: /Report text/ }).click()
    const dialog = teacher.getByRole('dialog', { name: 'Report text for Rina Putri' })
    await dialog.getByLabel('What the student achieved in this subject').fill('Works carefully and explains the steps of a solution.')
    await dialog.getByRole('button', { name: 'Save text' }).click()
    await expect(teacher.getByText('Report text saved')).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/grade-book.png', fullPage: true })

    // Before the grades are published the families see nothing, and report cards cannot be generated yet.
    await loginAs(student, 'Student')
    await student.getByRole('link', { name: 'My grades', exact: true }).first().click()
    await expect(student.getByText('Not published yet')).toBeVisible()
    await expect(teacher.getByRole('button', { name: 'Generate report cards' })).toBeDisabled()

    // The homeroom teacher publishes, which locks the scores, and generates the PDFs.
    await teacher.getByRole('button', { name: 'Publish grades' }).click()
    await teacher.getByRole('alertdialog').getByRole('button', { name: 'Publish the grades of this class?' }).click()
    await expect(teacher.getByText('Grades published')).toBeVisible()
    await expect(teacher.getByLabel('Midterm score of Rina Putri')).toBeDisabled()
    await teacher.getByRole('button', { name: 'Generate report cards' }).click()
    await teacher.getByRole('alertdialog').getByRole('button', { name: 'Generate the report cards?' }).click()
    // A PDF per student of the class is drawn and stored, which takes a few seconds.
    await expect(teacher.getByText(/report cards? generated/)).toBeVisible({ timeout: 45_000 })

    // The student sees the grades and the report text, and opens the PDF through a short-lived storage address.
    await student.reload()
    await loginAs(student, 'Student')
    await student.getByRole('link', { name: 'My grades', exact: true }).first().click()
    await expect(student.getByRole('cell', { name: 'Mathematics' })).toBeVisible()
    await expect(student.getByText('Works carefully and explains the steps of a solution.')).toBeVisible()
    // The PDF opens in a new tab; headless Chromium turns it into a download, so the request to the storage is what
    // is checked: the address must be a signed one of the private bucket.
    const pdfRequest = studentContext.waitForEvent('request', (request) => /\/schomans-private\/report-cards\/.+\.pdf\?/.test(request.url()))
    await student.getByRole('button', { name: 'Download report card (PDF)' }).click()
    expect((await pdfRequest).url()).toContain('X-Amz-Signature=')
    for (const page of studentContext.pages()) {
      if (page !== student) {
        await page.close()
      }
    }
    await student.screenshot({ path: 'test-results/screens/student-grades.png', fullPage: true })

    // The parent follows the same on the pages of the child.
    await loginAs(parent, 'Parent')
    await parent.getByRole('link', { name: 'Grades', exact: true }).first().click()
    await expect(parent.getByRole('heading', { level: 1, name: 'Grades of my child' })).toBeVisible()
    await expect(parent.getByRole('cell', { name: 'Mathematics' })).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/parent-grades.png', fullPage: true })

    // Take the grades back, so the scores are open again and the test can run again.
    await teacher.getByRole('button', { name: 'Unpublish grades' }).click()
    await teacher.getByRole('alertdialog').getByRole('button', { name: 'Unpublish the grades?' }).click()
    await expect(teacher.getByText('Grades unpublished')).toBeVisible()
  } finally {
    await Promise.all([teacherContext.close(), studentContext.close(), parentContext.close()])
  }
})

test('the dashboard shows the charts of each role', async ({ browser }) => {
  const principalContext = await browser.newContext()
  const teacherContext = await browser.newContext()
  const parentContext = await browser.newContext()

  try {
    const principal = await principalContext.newPage()
    await loginAs(principal, 'Principal')
    await expect(principal.getByRole('heading', { name: 'Attendance per class' })).toBeVisible()
    await expect(principal.getByRole('heading', { name: 'Average grade per subject' })).toBeVisible()
    await principal.screenshot({ path: 'test-results/screens/dashboard-principal.png', fullPage: true })

    const teacher = await teacherContext.newPage()
    await loginAs(teacher, 'Teacher')
    await expect(teacher.getByRole('heading', { name: 'Students who need attention' })).toBeVisible()
    await teacher.screenshot({ path: 'test-results/screens/dashboard-teacher.png', fullPage: true })

    const parent = await parentContext.newPage()
    await loginAs(parent, 'Parent')
    await expect(parent.getByRole('heading', { name: /^Progress of / }).first()).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/dashboard-parent.png', fullPage: true })
  } finally {
    await Promise.all([principalContext.close(), teacherContext.close(), parentContext.close()])
  }
})
