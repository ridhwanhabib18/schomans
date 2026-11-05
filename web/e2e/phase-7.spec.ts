import { expect, test } from '@playwright/test'
import { adminApi, loginAs, tokenFor, unique } from './support/helpers.ts'

const api = process.env.E2E_API_URL ?? 'http://localhost:5080'

// Phase 7 (mock AI provider, real services): the AI drafts a text that a person still reads and saves, the chatbot answers only
// from what the school published, and an online quiz is built from the question bank, taken once and scored by the teacher.
test('the AI drafts an announcement into the editor and nothing is saved until the person saves it', async ({ page }) => {
  await loginAs(page, 'Admin staff')
  await page.getByRole('link', { name: 'Manage announcements' }).first().click()
  await page.getByRole('link', { name: 'New announcement' }).click()

  await page.getByRole('button', { name: 'Draft with AI' }).click()
  const dialog = page.getByRole('dialog', { name: 'Draft the announcement with AI' })
  await dialog.getByLabel(/^Who is it for/).fill('Parents of class 7A')
  await dialog.getByLabel(/^Short notes/).fill('The school trip is on Friday at 7 AM. Bring a packed lunch.')
  await page.screenshot({ path: 'test-results/phase-7-draft-dialog.png', fullPage: true })
  await dialog.getByRole('button', { name: 'Write a draft' }).click()

  await expect(dialog).toHaveCount(0)
  await expect(page.getByLabel(/^Title/)).not.toHaveValue('')
  await expect(page.getByRole('textbox', { name: 'Announcement message' })).toContainText(/\S/)
  // The draft is only in the editor: the page is still the "new announcement" page.
  await expect(page).toHaveURL(/\/manage\/announcements\/new$/)
  await page.screenshot({ path: 'test-results/phase-7-draft-in-editor.png', fullPage: true })
})

test('the chatbot answers a guest from a published announcement with its source, and refuses what it does not know', async ({ browser, request }) => {
  const token = unique()
  const title = `E2E open house ${token}`
  const sentence = `The pelikan${token} open house is on Saturday in the main hall.`
  let id: string | undefined
  const admin = await browser.newContext()
  const guest = await browser.newContext()

  try {
    const adminPage = await admin.newPage()
    await loginAs(adminPage, 'Admin staff')
    await adminPage.getByRole('link', { name: 'Manage announcements' }).first().click()
    await adminPage.getByRole('link', { name: 'New announcement' }).click()
    await adminPage.getByLabel(/^Title/).fill(title)
    await adminPage.getByRole('textbox', { name: 'Announcement message' }).click()
    await adminPage.keyboard.type(sentence)
    await adminPage.getByRole('checkbox', { name: 'Also show on the public pages' }).check()
    await adminPage.getByRole('button', { name: 'Save draft' }).click()
    await expect(adminPage).toHaveURL(/\/manage\/announcements\/[0-9a-f-]{36}$/)
    id = /\/manage\/announcements\/([0-9a-f-]{36})$/.exec(adminPage.url())![1]
    await adminPage.getByRole('button', { name: 'Publish' }).click()
    await adminPage.getByRole('alertdialog').getByRole('button', { name: 'Publish' }).click()
    await expect(adminPage).toHaveURL(/\/manage\/announcements$/)

    const guestPage = await guest.newPage()
    await guestPage.goto('/')
    const widget = guestPage.getByRole('region', { name: 'Ask the school' })
    await widget.getByLabel('Your question').fill(`When is the pelikan${token} open house?`)
    await widget.getByRole('button', { name: 'Ask' }).click()
    await expect(widget.getByText(sentence)).toBeVisible()
    await expect(widget.getByRole('link', { name: new RegExp(title) })).toBeVisible()
    await guestPage.screenshot({ path: 'test-results/phase-7-chatbot-answer.png', fullPage: true })

    await widget.getByLabel('Your question').fill('What is the capital of Mongolia xqzvw?')
    await widget.getByRole('button', { name: 'Ask' }).click()
    await expect(widget.getByText(/I could not find this in the information the school has published/)).toBeVisible()
    await guestPage.screenshot({ path: 'test-results/phase-7-chatbot-refusal.png', fullPage: true })
  } finally {
    if (id) {
      await (await adminApi(request)).del(`/api/v1/announcements/${id}`)
    }
    await Promise.all([admin.close(), guest.close()])
  }
})

test('a teacher builds a quiz from the question bank, a student takes it once, and the teacher scores the short answer', async ({ browser, request }) => {
  test.setTimeout(180_000)
  const token = unique()
  const title = `E2E quiz ${token}`
  const teacherHeaders = { Authorization: `Bearer ${await tokenFor(request, 'Teacher')}` }

  // Two questions in the bank of Mathematics, made through the API (the PDF generator has its own tests).
  const options = (await (await request.get(`${api}/api/v1/assignments/teaching-options`, { headers: teacherHeaders })).json()) as {
    classroomName: string
    subjectId: string
    subjectName: string
  }[]
  const subject = options.find((option) => option.subjectName === 'Mathematics' && option.classroomName === 'Class A')
  expect(subject, 'the demo teacher teaches Mathematics in Class A').toBeDefined()
  const multipleChoice = `What is 7 times 8 (${token})?`
  const shortAnswer = `Explain what a fraction is (${token}).`
  const saved = await request.post(`${api}/api/v1/question-bank`, {
    headers: teacherHeaders,
    data: {
      subjectId: subject!.subjectId,
      questions: [
        { type: 'MultipleChoice', difficulty: 'Easy', body: multipleChoice, options: ['54', '56', '64'], answerKey: '1', explanation: '7 x 8 = 56.' },
        { type: 'ShortAnswer', difficulty: 'Medium', body: shortAnswer, options: [], answerKey: 'A part of a whole.', explanation: 'A fraction is a part of a whole.' },
      ],
    },
  })
  expect(saved.status()).toBe(201)

  const teacherContext = await browser.newContext()
  const studentContext = await browser.newContext()
  let quizId: string | undefined
  try {
    const teacher = await teacherContext.newPage()
    const student = await studentContext.newPage()
    await loginAs(student, 'Student')
    await loginAs(teacher, 'Teacher')

    // The teacher builds the quiz: subject, classroom, due date in the future, and the two questions of the bank.
    await teacher.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.getByRole('link', { name: 'New quiz' }).click()
    await expect(teacher.getByRole('heading', { level: 1, name: 'New quiz' })).toBeVisible()
    await teacher.getByLabel(/^Title of the quiz/).fill(title)
    await teacher.getByLabel(/^Subject/).selectOption({ label: 'Mathematics' })
    await teacher.getByLabel('Class A').check()
    const due = new Date(Date.now() + 2 * 86_400_000).toISOString().slice(0, 16)
    await teacher.getByLabel(/^Closes at/).fill(due)
    await teacher.getByLabel(/^Search in the text/).fill(token)
    await teacher.getByRole('checkbox', { name: new RegExp(multipleChoice.replace(/[()?]/g, '\\$&')) }).check()
    await teacher.getByRole('checkbox', { name: new RegExp(shortAnswer.replace(/[()?]/g, '\\$&')) }).check()
    await expect(teacher.getByText('In the quiz: 2 question(s), 2 point(s)')).toBeVisible()
    await teacher.screenshot({ path: 'test-results/phase-7-quiz-builder.png', fullPage: true })
    await teacher.getByRole('button', { name: 'Create the quiz' }).click()
    await expect(teacher.getByRole('heading', { level: 1, name: title })).toBeVisible()
    quizId = /\/assignments\/([0-9a-f-]{36})$/.exec(teacher.url())?.[1]

    // The student is told, opens the quiz, answers both questions and hands it in after a confirmation.
    await expect(student.getByRole('status').getByText(`New assignment: ${title}`)).toBeVisible()
    await student.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await student.getByRole('link', { name: new RegExp(title) }).click()
    await expect(student.getByText(/You can take this quiz only once/)).toBeVisible()
    await student.getByRole('radio', { name: '56' }).check()
    await student.getByLabel('Your answer to question 2').fill('A part of a whole.')
    await student.screenshot({ path: 'test-results/phase-7-quiz-student.png', fullPage: true })
    await student.getByRole('button', { name: 'Hand in the quiz' }).click()
    await student.getByRole('alertdialog').getByRole('button', { name: 'Hand in the quiz' }).click()
    // Before the due date the student only sees that the quiz is in; the score and the key come after it.
    await expect(student.getByText(/Your quiz is handed in\. The result is shown after the due date/)).toBeVisible()
    await expect(student.getByText('Right answer')).toHaveCount(0)
    await student.screenshot({ path: 'test-results/phase-7-quiz-handed-in.png', fullPage: true })

    // The teacher sees the attempt, waiting for points, and scores the short answer.
    // Tokens live in memory, so go back through the app instead of reloading the page.
    await teacher.getByRole('link', { name: 'Assignments', exact: true }).first().click()
    await teacher.getByRole('link', { name: new RegExp(title) }).click()
    const row = teacher.getByRole('row', { name: /Rina Putri/ })
    await expect(row).toContainText('Submitted')
    await row.getByRole('button', { name: /^Review/ }).click()
    const dialog = teacher.getByRole('dialog', { name: 'Work of Rina Putri' })
    await expect(dialog.getByText('1 short answer(s) wait for points from the teacher.')).toBeVisible()
    await dialog.getByLabel('Points for question 2 (0 to 1)').fill('1')
    await dialog.getByRole('button', { name: 'Save the points' }).click()
    await expect(teacher.getByText('Points saved.')).toBeVisible()
    await teacher.screenshot({ path: 'test-results/phase-7-quiz-teacher-scored.png', fullPage: true })
  } finally {
    if (quizId) {
      await (await adminApi(request)).del(`/api/v1/assignments/${quizId}`)
    }
    await Promise.all([teacherContext.close(), studentContext.close()])
  }
})
