import { expect, test } from '@playwright/test'
import { adminApi, loginAs } from './support/helpers.ts'

/** Removes the Saturday lessons of Class C, which only this test creates: a failed run must not block the next one. */
async function clearSaturdays(request: import('@playwright/test').APIRequestContext) {
  const api = await adminApi(request)
  const classrooms = await api.get<{ items: { id: string; name: string }[] }>('/api/v1/classrooms?pageSize=100&search=Class C')
  const classC = classrooms.items.find((c) => c.name === 'Class C')!
  const lessons = await api.get<{ items: { id: string }[] }>(`/api/v1/schedules?classroomId=${classC.id}&dayOfWeek=Saturday&pageSize=100`)
  for (const lesson of lessons.items) await api.del(`/api/v1/schedules/${lesson.id}`)
}


test('the admin adds a lesson to the timetable, a clash is refused, and the lesson can be removed', async ({ page, request }) => {
  await clearSaturdays(request)
  test.info().annotations.push({ type: 'cleanup', description: 'Saturday lessons of Class C are removed before and after' })
  await loginAs(page, 'Admin staff')
  await page.getByRole('link', { name: 'Timetable', exact: true }).first().click()
  await expect(page.getByRole('heading', { level: 1, name: 'Timetable' })).toBeVisible()
  await page.getByLabel('Classroom', { exact: true }).selectOption({ label: 'Class C' })
  await expect(page.getByRole('heading', { name: 'Monday' })).toBeVisible()
  await page.screenshot({ path: 'test-results/screens/timetable-week.png', fullPage: true })

  // A Saturday evening slot nobody uses, so the weekend column appears with the new lesson.
  await page.getByRole('button', { name: 'Add lesson' }).click()
  let dialog = page.getByRole('dialog', { name: 'Add lesson' })
  await dialog.getByLabel(/^Day/).selectOption('Saturday')
  await dialog.getByLabel(/Starts at/).fill('19:00')
  await dialog.getByLabel(/Ends at/).fill('19:30')
  await dialog.getByLabel(/^Subject/).selectOption({ label: 'Arts and Culture' })
  await dialog.getByLabel(/^Teacher/).selectOption({ index: 1 })
  await dialog.getByRole('button', { name: 'Save' }).click()
  await expect(dialog).toBeHidden()
  const saturday = page.getByRole('region', { name: 'Saturday' })
  await expect(saturday.locator('p.font-medium', { hasText: 'Arts and Culture' })).toBeVisible()
  await expect(saturday.getByText('19:00–19:30')).toBeVisible()

  // A second lesson at an overlapping time in the same classroom is refused with the reason.
  await page.getByRole('button', { name: 'Add lesson' }).click()
  dialog = page.getByRole('dialog', { name: 'Add lesson' })
  await dialog.getByLabel(/^Day/).selectOption('Saturday')
  await dialog.getByLabel(/Starts at/).fill('19:15')
  await dialog.getByLabel(/Ends at/).fill('19:45')
  await dialog.getByLabel(/^Subject/).selectOption({ label: 'English' })
  await dialog.getByLabel(/^Teacher/).selectOption({ index: 2 })
  await dialog.getByRole('button', { name: 'Save' }).click()
  await expect(dialog.getByText('The classroom already has a lesson at this time.')).toBeVisible()
  await dialog.getByRole('button', { name: 'Cancel' }).click()

  // The lesson is removed again, which leaves the demo timetable as it was.
  await saturday.getByRole('button', { name: /^Delete: Arts and Culture/ }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Delete lesson' }).click()
  await expect(page.getByRole('heading', { name: 'Saturday' })).toHaveCount(0)
  await clearSaturdays(request)
})

test('a teacher reads the timetable without being able to change it', async ({ page }) => {
  await loginAs(page, 'Teacher')
  await page.getByRole('link', { name: 'Timetable', exact: true }).first().click()

  await expect(page.getByRole('heading', { level: 1, name: 'Timetable' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Add lesson' })).toHaveCount(0)
})
