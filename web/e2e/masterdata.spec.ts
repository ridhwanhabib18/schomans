import { expect, test, type Page } from '@playwright/test'
import { adminApi, loginAs, unique } from './support/helpers.ts'
import { createXlsx } from './support/xlsx.ts'

interface Paged<T> {
  items: T[]
}

const run = unique()
// Every record of this run carries the run number, so cleanup finds exactly what the test created.
const teacherName = `E2E Teacher ${run}`
const subjectName = `E2E Subject ${run}`
const classroomName = `E2E-${run.slice(-8)}`
const studentName = `E2E Student ${run}`
// Ten digits for a NISN, eighteen for a NIP: the run number (13 digits) makes them unique.
const nisn = run.slice(-10)
const nip = `20${run}`.padEnd(18, '0')

async function openFromNavigation(page: Page, link: string, heading: string) {
  await page.getByRole('link', { name: link, exact: true }).first().click()
  await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible()
}

async function search(page: Page, text: string) {
  await page.getByRole('searchbox').fill(text)
}

async function dialog(page: Page, name: string) {
  const found = page.getByRole('dialog', { name })
  await expect(found).toBeVisible()
  return found
}

// One story in order: the admin builds a small school piece by piece, imports students, then removes it all again.
test.describe.serial('master data', () => {
  test.afterAll(async ({ request }) => {
    // Whatever a failed step left behind is removed here, students first (a classroom with students cannot be deleted).
    const api = await adminApi(request)
    const students = await api.get<Paged<{ id: string }>>(`/api/v1/students?page=1&pageSize=100&search=E2E`)
    for (const student of students.items) await api.del(`/api/v1/students/${student.id}`)
    const classrooms = await api.get<Paged<{ id: string }>>(`/api/v1/classrooms?page=1&pageSize=100&search=${classroomName}`)
    for (const classroom of classrooms.items) await api.del(`/api/v1/classrooms/${classroom.id}`)
    const subjects = await api.get<Paged<{ id: string }>>(`/api/v1/subjects?page=1&pageSize=100&search=${subjectName}`)
    for (const subject of subjects.items) await api.del(`/api/v1/subjects/${subject.id}`)
    const teachers = await api.get<Paged<{ id: string }>>(`/api/v1/teachers?page=1&pageSize=100&search=${teacherName}`)
    for (const teacher of teachers.items) await api.del(`/api/v1/teachers/${teacher.id}`)
  })

  test('the admin adds a teacher, a subject and a classroom', async ({ page }) => {
    await loginAs(page, 'Admin staff')

    await openFromNavigation(page, 'Teachers', 'Teachers')
    await page.getByRole('button', { name: 'Add teacher' }).click()
    let form = await dialog(page, 'Add teacher')
    await form.getByLabel(/Full name/).fill(teacherName)
    await form.getByLabel(/^Email/).fill(`e2e.teacher.${run}@schomans.demo`)
    await form.getByLabel(/NIP/).fill(nip)
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form).toBeHidden()
    await search(page, teacherName)
    await expect(page.getByRole('cell', { name: teacherName, exact: true })).toBeVisible()

    await openFromNavigation(page, 'Subjects', 'Subjects')
    await page.getByRole('button', { name: 'Add subject' }).click()
    form = await dialog(page, 'Add subject')
    await form.getByLabel(/^Name/).fill(subjectName)
    await form.getByLabel(/Minimum passing score/).fill('72.5')
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form).toBeHidden()
    await search(page, subjectName)
    await expect(page.getByRole('cell', { name: subjectName, exact: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: '72.5', exact: true })).toBeVisible()

    await openFromNavigation(page, 'Classrooms', 'Classrooms')
    await page.getByRole('button', { name: 'Add classroom' }).click()
    form = await dialog(page, 'Add classroom')
    await form.getByLabel(/^Name/).fill(classroomName)
    await expect(form.getByLabel(/Academic year/)).toHaveValue(/[0-9a-f-]{36}/) // the current year is preselected
    await form.getByLabel(/Homeroom teacher/).selectOption({ label: teacherName })
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form).toBeHidden()
    await search(page, classroomName)
    await expect(page.getByRole('row', { name: new RegExp(`${classroomName}.*2026/2027.*${teacherName}`) })).toBeVisible()
    await page.screenshot({ path: 'test-results/screens/masterdata-classrooms.png', fullPage: true })
  })

  test('the admin adds, finds, edits a student, and the form explains a duplicate NISN', async ({ page }) => {
    await loginAs(page, 'Admin staff')
    await openFromNavigation(page, 'Students', 'Students')

    await page.getByRole('button', { name: 'Add student' }).click()
    let form = await dialog(page, 'Add student')
    await form.getByLabel(/Full name/).fill(studentName)
    await form.getByLabel(/^Email/).fill(`e2e.student.${run}@schomans.demo`)
    await form.getByLabel(/NISN/).fill(nisn)
    await form.getByLabel(/Classroom/).selectOption({ label: classroomName })
    await form.getByLabel(/Date of birth/).fill('2010-03-04')
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form).toBeHidden()

    await search(page, studentName)
    const row = page.getByRole('row', { name: new RegExp(studentName) })
    await expect(row).toContainText(nisn)
    await expect(row).toContainText(classroomName)
    await page.screenshot({ path: 'test-results/screens/masterdata-students.png', fullPage: true })

    // The same NISN again is a business error, shown in the form in plain words.
    await page.getByRole('button', { name: 'Add student' }).click()
    form = await dialog(page, 'Add student')
    await form.getByLabel(/Full name/).fill(`${studentName} copy`)
    await form.getByLabel(/^Email/).fill(`e2e.copy.${run}@schomans.demo`)
    await form.getByLabel(/NISN/).fill(nisn)
    await form.getByLabel(/Classroom/).selectOption({ label: classroomName })
    await form.getByLabel(/Date of birth/).fill('2010-03-05')
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form.getByText('Another student already has this NISN.')).toBeVisible()
    await form.getByRole('button', { name: 'Cancel' }).click()

    // Edit the name.
    await row.getByRole('button', { name: /^Edit/ }).click()
    form = await dialog(page, 'Edit student')
    await form.getByLabel(/Full name/).fill(`${studentName} edited`)
    await form.getByRole('button', { name: 'Save' }).click()
    await expect(form).toBeHidden()
    await expect(page.getByRole("cell", { name: `${studentName} edited`, exact: true })).toBeVisible()
  })

  test('the admin imports students from an Excel file, and the failed rows are listed', async ({ page }) => {
    await loginAs(page, 'Admin staff')
    await openFromNavigation(page, 'Students', 'Students')

    const file = createXlsx('Students', [
      ['Nisn', 'FullName', 'Email', 'DateOfBirth', 'Classroom'],
      [`8${run.slice(-9)}`, `E2E Import One ${run}`, `e2e.import1.${run}@schomans.demo`, '2010-01-15', classroomName],
      ['123', `E2E Import Two ${run}`, `e2e.import2.${run}@schomans.demo`, '2010-02-16', classroomName],
      [`9${run.slice(-9)}`, `E2E Import Three ${run}`, `e2e.import3.${run}@schomans.demo`, '2010-03-17', classroomName],
    ])

    await page.getByRole('button', { name: 'Import from Excel' }).click()
    const form = await dialog(page, 'Import students from Excel')
    await form.getByLabel(/Excel file/).setInputFiles({ name: 'students.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: file })
    await form.getByRole('button', { name: 'Import' }).click()

    // The dialog changes its title to "Import finished" when the result arrives.
    const result = page.getByRole('dialog', { name: 'Import finished' })
    await expect(result.getByRole('status')).toContainText('2 of 3 rows were imported.')
    const failed = result.getByRole('table', { name: 'Rows that were not imported' })
    await expect(failed.getByRole('cell', { name: '3', exact: true })).toBeVisible()
    await expect(failed).toContainText('NISN')
    await page.screenshot({ path: 'test-results/screens/masterdata-import-result.png' })
    await result.getByRole('button', { name: 'Close' }).first().click()

    // The two good rows are in the list now.
    await search(page, `E2E Import`)
    await expect(page.getByRole('cell', { name: `E2E Import One ${run}`, exact: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: `E2E Import Three ${run}`, exact: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: `E2E Import Two ${run}`, exact: true })).toHaveCount(0)

    // A wrong file type is refused before anything is sent.
    await page.getByRole('button', { name: 'Import from Excel' }).click()
    const again = await dialog(page, 'Import students from Excel')
    await again.getByLabel(/Excel file/).setInputFiles({ name: 'students.csv', mimeType: 'text/csv', buffer: Buffer.from('a,b') })
    await again.getByRole('button', { name: 'Import' }).click()
    await expect(again.getByText('Choose an .xlsx file.')).toBeVisible()
  })

  test('the admin deletes what was added, and a classroom with students cannot be deleted', async ({ page }) => {
    await loginAs(page, 'Admin staff')

    // The classroom still has students: the API refuses and the page says why.
    await openFromNavigation(page, 'Classrooms', 'Classrooms')
    await search(page, classroomName)
    await page.getByRole('row', { name: new RegExp(classroomName) }).getByRole('button', { name: /^Delete/ }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete classroom' }).click()
    await expect(page.getByText('This classroom still has students. Move them to another classroom first.')).toBeVisible()

    // Delete the students one by one through the screen.
    await openFromNavigation(page, 'Students', 'Students')
    await search(page, `E2E`)
    for (const text of [`${studentName} edited`, `E2E Import One ${run}`, `E2E Import Three ${run}`]) {
      await page.getByRole('row', { name: new RegExp(text) }).getByRole('button', { name: /^Delete/ }).click()
      await page.getByRole('alertdialog').getByRole('button', { name: 'Delete student' }).click()
      await expect(page.getByRole('cell', { name: text, exact: true })).toHaveCount(0)
    }

    await openFromNavigation(page, 'Classrooms', 'Classrooms')
    await search(page, classroomName)
    await page.getByRole('row', { name: new RegExp(classroomName) }).getByRole('button', { name: /^Delete/ }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete classroom' }).click()
    await expect(page.getByRole('cell', { name: classroomName, exact: true })).toHaveCount(0)

    // The teacher is homeroom teacher of nothing now, so can go too.
    await openFromNavigation(page, 'Teachers', 'Teachers')
    await search(page, teacherName)
    await page.getByRole('row', { name: new RegExp(teacherName) }).getByRole('button', { name: /^Delete/ }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete teacher' }).click()
    await expect(page.getByRole('cell', { name: teacherName, exact: true })).toHaveCount(0)

    await openFromNavigation(page, 'Subjects', 'Subjects')
    await search(page, subjectName)
    await page.getByRole('row', { name: new RegExp(subjectName) }).getByRole('button', { name: /^Delete/ }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete subject' }).click()
    await expect(page.getByRole('cell', { name: subjectName, exact: true })).toHaveCount(0)
  })
})

test.describe('who may see and change master data', () => {
  test('a teacher sees the students, without any button to change them, and not the teachers page', async ({ page }) => {
    await loginAs(page, 'Teacher')

    await openFromNavigation(page, 'Students', 'Students')
    await expect(page.getByRole('button', { name: 'Add student' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /^Edit/ })).toHaveCount(0)
    await expect(page.getByRole('link', { name: 'Teachers', exact: true })).toHaveCount(0)
    await page.screenshot({ path: 'test-results/screens/masterdata-teacher-view.png', fullPage: true })

    await page.evaluate(() => {
      window.history.pushState({}, '', '/manage/teachers')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })
    await expect(page.getByRole('alert')).toContainText('You do not have access to this page')
  })

  test('the principal sees everything and can change nothing', async ({ page }) => {
    await loginAs(page, 'Principal')

    await openFromNavigation(page, 'Teachers', 'Teachers')
    await expect(page.getByRole('button', { name: 'Add teacher' })).toHaveCount(0)
    await openFromNavigation(page, 'Academic years', 'Academic years')
    await expect(page.getByRole('link', { name: 'Start a new year' })).toHaveCount(0)
    await expect(page.getByText('Current', { exact: true })).toBeVisible()
  })

  test('a student has no management pages', async ({ page }) => {
    await loginAs(page, 'Student')

    for (const name of ['Students', 'Teachers', 'Classrooms', 'Subjects', 'Academic years', 'Manage news', 'Manage announcements']) {
      await expect(page.getByRole('link', { name, exact: true })).toHaveCount(0)
    }
    await expect(page.getByRole('link', { name: 'Announcements', exact: true }).first()).toBeVisible()
  })

  test('the academic year page lists the current year with its terms; rollover is not run here', async ({ page }) => {
    await loginAs(page, 'Admin staff')

    await openFromNavigation(page, 'Academic years', 'Academic years')
    await expect(page.getByRole('row', { name: /2026\/2027/ })).toContainText('Current')
    // The wizard opens and validates, but nothing is confirmed: a rollover would change the shared demo data for good.
    await page.getByRole('link', { name: 'Start a new year' }).click()
    await expect(page.getByRole('heading', { name: 'The new academic year' })).toBeVisible()
    await page.getByRole('button', { name: 'Next' }).click()
    await expect(page.getByRole('alert')).toContainText('Enter a name for the new academic year.')
    await page.screenshot({ path: 'test-results/screens/rollover-wizard-validation.png', fullPage: true })
  })
})
